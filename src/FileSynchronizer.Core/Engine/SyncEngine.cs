using System.Diagnostics;
using System.IO;
using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Models;
using FileSynchronizer.Core.State;

namespace FileSynchronizer.Core.Engine;

/// <summary>
/// Core synchronization engine supporting 1-way backup and 2-way bi-directional synchronization.
/// </summary>
public class SyncEngine : ISyncEngine
{
    private const int CopyBufferSize = 1024 * 512; // 512 KB copy buffer
    private static readonly TimeSpan FatTimestampTolerance = TimeSpan.FromSeconds(2); // FAT filesystem timestamp tolerance

    private readonly IUsbDeviceWatcher _usbWatcher;
    private readonly ISyncStateStore _stateStore;

    public SyncEngine(IUsbDeviceWatcher usbWatcher, ISyncStateStore stateStore)
    {
        _usbWatcher = usbWatcher;
        _stateStore = stateStore;
    }

    public async Task<SyncPlan> AnalyzeAsync(
        SyncJob job,
        IProgress<SyncProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        var connectedDrives = _usbWatcher.GetConnectedDrives();

        // 1. Resolve paths
        var leftRes = UsbPathResolver.ResolvePath(job.LeftPath, connectedDrives);
        if (!leftRes.Success)
        {
            throw new DirectoryNotFoundException($"Błąd ścieżki źródłowej (Left): {leftRes.ErrorMessage}");
        }

        var rightRes = UsbPathResolver.ResolvePath(job.RightPath, connectedDrives);
        if (!rightRes.Success)
        {
            throw new DirectoryNotFoundException($"Błąd ścieżki docelowej (Right): {rightRes.ErrorMessage}");
        }

        var leftPath = leftRes.ResolvedPath;
        var rightPath = rightRes.ResolvedPath;

        if (!Directory.Exists(leftPath))
        {
            throw new DirectoryNotFoundException($"Folder źródłowy nie istnieje: {leftPath}");
        }

        // 2. Scan both sides
        progress?.Report(new SyncProgressReport
        {
            Phase = "Skanowanie folderu źródłowego (Left)...",
            CurrentItemPath = leftPath
        });

        var scanProgress = new Progress<string>(msg => progress?.Report(new SyncProgressReport { Phase = msg }));
        var leftFiles = await FileScanner.ScanDirectoryAsync(leftPath, job.ExcludePatterns, job.IncludePatterns, scanProgress, ct);

        progress?.Report(new SyncProgressReport
        {
            Phase = "Skanowanie folderu docelowego (Right)...",
            CurrentItemPath = rightPath
        });

        var rightFiles = Directory.Exists(rightPath)
            ? await FileScanner.ScanDirectoryAsync(rightPath, job.ExcludePatterns, job.IncludePatterns, scanProgress, ct)
            : new Dictionary<string, FileMetadata>(StringComparer.OrdinalIgnoreCase);

        // 3. Load snapshot (previous state)
        var snapshot = await _stateStore.LoadSnapshotAsync(job.Id, ct);
        var snapshotEntries = snapshot?.Entries ?? new Dictionary<string, FileStateEntry>(StringComparer.OrdinalIgnoreCase);

        // 4. Compare all relative paths
        progress?.Report(new SyncProgressReport
        {
            Phase = "Porównywanie i analiza różnic..."
        });

        var allRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in leftFiles.Keys) allRelativePaths.Add(k);
        foreach (var k in rightFiles.Keys) allRelativePaths.Add(k);
        if (job.Direction == SyncDirection.TwoWaySync)
        {
            foreach (var k in snapshotEntries.Keys) allRelativePaths.Add(k);
        }

        var items = new List<SyncItem>();
        int processedCount = 0;
        int totalToAnalyze = allRelativePaths.Count;

        foreach (var relPath in allRelativePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            processedCount++;

            leftFiles.TryGetValue(relPath, out var leftFile);
            rightFiles.TryGetValue(relPath, out var rightFile);
            snapshotEntries.TryGetValue(relPath, out var prevEntry);

            var item = new SyncItem
            {
                RelativePath = relPath,
                LeftFile = leftFile,
                RightFile = rightFile
            };

            if (job.Direction == SyncDirection.OneWayBackup)
            {
                await AnalyzeOneWayBackupItemAsync(job, item, leftFile, rightFile, ct);
            }
            else
            {
                await AnalyzeTwoWaySyncItemAsync(job, item, leftFile, rightFile, prevEntry, ct);
            }

            items.Add(item);

            if (processedCount % 100 == 0 || processedCount == totalToAnalyze)
            {
                progress?.Report(new SyncProgressReport
                {
                    Phase = "Analiza...",
                    ProcessedItems = processedCount,
                    TotalItems = totalToAnalyze,
                    CurrentItemPath = relPath
                });
            }
        }

        return new SyncPlan
        {
            JobId = job.Id,
            ResolvedLeftPath = leftPath,
            ResolvedRightPath = rightPath,
            Items = items
        };
    }

    private static async Task AnalyzeOneWayBackupItemAsync(
        SyncJob job,
        SyncItem item,
        FileMetadata? left,
        FileMetadata? right,
        CancellationToken ct)
    {
        if (left != null && right == null)
        {
            // New file on Left
            item.Action = SyncActionType.CopyLeftToRight;
            item.StatusDescription = "Nowy plik po lewej stronie";
        }
        else if (left == null && right != null)
        {
            // Deleted on Left
            if (job.DeleteOrphanFilesOnTarget)
            {
                item.Action = SyncActionType.DeleteRight;
                item.StatusDescription = "Usunięty w źródle (do skasowania z celu)";
            }
            else
            {
                item.Action = SyncActionType.InSync;
                item.StatusDescription = "Plik istnieje tylko w kopii (zachowany)";
            }
        }
        else if (left != null && right != null)
        {
            // Exists on both sides - check if identical
            bool isIdentical = await AreFilesIdenticalAsync(left, right, job.CompareMethod, ct);
            if (isIdentical)
            {
                item.Action = SyncActionType.InSync;
                item.StatusDescription = "Pliki są identyczne";
            }
            else
            {
                item.Action = SyncActionType.CopyLeftToRight;
                item.StatusDescription = left.LastModifiedUtc > right.LastModifiedUtc
                    ? "Zaktualizowany po lewej stronie (nowszy)"
                    : "Zmieniony po lewej stronie";
            }
        }
    }

    private static async Task AnalyzeTwoWaySyncItemAsync(
        SyncJob job,
        SyncItem item,
        FileMetadata? left,
        FileMetadata? right,
        FileStateEntry? prev,
        CancellationToken ct)
    {
        if (left != null && right != null)
        {
            bool isIdentical = await AreFilesIdenticalAsync(left, right, job.CompareMethod, ct);
            if (isIdentical)
            {
                item.Action = SyncActionType.InSync;
                item.StatusDescription = "Pliki są identyczne";
                return;
            }

            // Files are different
            if (prev == null)
            {
                // No snapshot history: apply conflict policy
                ResolveConflict(job, item, left, right, "Brak historii synchronizacji, oba pliki istnieją i różnią się");
                return;
            }

            bool leftChanged = IsFileChangedFromSnapshot(left, prev);
            bool rightChanged = IsFileChangedFromSnapshot(right, prev);

            if (leftChanged && !rightChanged)
            {
                item.Action = SyncActionType.CopyLeftToRight;
                item.StatusDescription = "Zmodyfikowany po lewej stronie";
            }
            else if (!leftChanged && rightChanged)
            {
                item.Action = SyncActionType.CopyRightToLeft;
                item.StatusDescription = "Zmodyfikowany po prawej stronie";
            }
            else
            {
                // Both changed
                ResolveConflict(job, item, left, right, "Plik został zmodyfikowany po obu stronach");
            }
        }
        else if (left != null && right == null)
        {
            if (prev == null || !prev.ExistedOnRight)
            {
                // Newly created on Left
                item.Action = SyncActionType.CopyLeftToRight;
                item.StatusDescription = "Nowy plik po lewej stronie";
            }
            else
            {
                // Existed on Right in snapshot, but now missing -> deleted on Right
                bool leftChanged = IsFileChangedFromSnapshot(left, prev);
                if (!leftChanged)
                {
                    item.Action = SyncActionType.DeleteLeft;
                    item.StatusDescription = "Usunięty po prawej stronie (do usunięcia po lewej)";
                }
                else
                {
                    ResolveConflict(job, item, left, null, "Zmodyfikowany po lewej, usunięty po prawej");
                }
            }
        }
        else if (left == null && right != null)
        {
            if (prev == null || !prev.ExistedOnLeft)
            {
                // Newly created on Right
                item.Action = SyncActionType.CopyRightToLeft;
                item.StatusDescription = "Nowy plik po prawej stronie";
            }
            else
            {
                // Existed on Left in snapshot, but now missing -> deleted on Left
                bool rightChanged = IsFileChangedFromSnapshot(right, prev);
                if (!rightChanged)
                {
                    item.Action = SyncActionType.DeleteRight;
                    item.StatusDescription = "Usunięty po lewej stronie (do usunięcia po prawej)";
                }
                else
                {
                    ResolveConflict(job, item, null, right, "Zmodyfikowany po prawej, usunięty po lewej");
                }
            }
        }
        else
        {
            // Both deleted
            item.Action = SyncActionType.InSync;
            item.StatusDescription = "Usunięty z obu stron";
        }
    }

    private static void ResolveConflict(
        SyncJob job,
        SyncItem item,
        FileMetadata? left,
        FileMetadata? right,
        string reason)
    {
        switch (job.ConflictPolicy)
        {
            case ConflictResolutionPolicy.LeftWins:
                item.Action = SyncActionType.CopyLeftToRight;
                item.StatusDescription = $"{reason} (Reguła: Lewa strona wygrywa)";
                break;
            case ConflictResolutionPolicy.RightWins:
                item.Action = SyncActionType.CopyRightToLeft;
                item.StatusDescription = $"{reason} (Reguła: Prawa strona wygrywa)";
                break;
            case ConflictResolutionPolicy.NewerWins:
                if (left != null && right != null)
                {
                    if (left.LastModifiedUtc >= right.LastModifiedUtc)
                    {
                        item.Action = SyncActionType.CopyLeftToRight;
                        item.StatusDescription = $"{reason} (Reguła: Nowszy plik z lewej strony)";
                    }
                    else
                    {
                        item.Action = SyncActionType.CopyRightToLeft;
                        item.StatusDescription = $"{reason} (Reguła: Nowszy plik z prawej strony)";
                    }
                }
                else
                {
                    item.Action = left != null ? SyncActionType.CopyLeftToRight : SyncActionType.CopyRightToLeft;
                }
                break;
            case ConflictResolutionPolicy.Manual:
            default:
                item.Action = SyncActionType.Conflict;
                item.StatusDescription = $"{reason} (Wymaga decyzji użytkownika)";
                break;
        }
    }

    private static bool IsFileChangedFromSnapshot(FileMetadata current, FileStateEntry snapshot)
    {
        if (current.Size != snapshot.Size) return true;
        var diff = (current.LastModifiedUtc - snapshot.LastModifiedUtc).Duration();
        return diff > FatTimestampTolerance;
    }

    private static async Task<bool> AreFilesIdenticalAsync(
        FileMetadata a,
        FileMetadata b,
        CompareMethod method,
        CancellationToken ct)
    {
        if (a.Size != b.Size)
        {
            return false;
        }

        var timeDiff = (a.LastModifiedUtc - b.LastModifiedUtc).Duration();
        bool dateMatches = timeDiff <= FatTimestampTolerance;

        if (method == CompareMethod.SizeAndDate)
        {
            return dateMatches;
        }

        if (method == CompareMethod.SizeAndDateWithHashFallback && dateMatches)
        {
            return true;
        }

        // Hash comparison required
        a.Hash ??= await FileHasher.ComputeXxHash64Async(a.FullPath, ct);
        b.Hash ??= await FileHasher.ComputeXxHash64Async(b.FullPath, ct);

        return string.Equals(a.Hash, b.Hash, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SyncResult> ExecuteAsync(
        SyncJob job,
        SyncPlan plan,
        IProgress<SyncProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int copiedCount = 0;
        int deletedCount = 0;
        int skippedCount = 0;
        int errorsCount = 0;
        long totalBytesTransferred = 0;
        var errors = new List<string>();

        var actionItems = plan.Items.Where(i => i.RequiresAction).ToList();
        int totalActions = actionItems.Count;
        int processedActions = 0;
        long totalBytesToTransfer = plan.TotalBytesToTransfer;

        foreach (var item in actionItems)
        {
            ct.ThrowIfCancellationRequested();
            processedActions++;
            item.Status = ItemSyncStatus.InProgress;

            try
            {
                switch (item.Action)
                {
                    case SyncActionType.CopyLeftToRight:
                    {
                        var src = Path.Combine(plan.ResolvedLeftPath, item.RelativePath);
                        var dst = Path.Combine(plan.ResolvedRightPath, item.RelativePath);

                        var transferred = await CopyFileSafelyAsync(src, dst, job.UseSafeAtomicCopy, (bytesInFile) =>
                        {
                            var totalNow = totalBytesTransferred + bytesInFile;
                            var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                            var speed = elapsedSec > 0 ? totalNow / elapsedSec : 0;

                            progress?.Report(new SyncProgressReport
                            {
                                Phase = "Kopiowanie (Lewa ➡️ Prawa)...",
                                CurrentItemPath = item.RelativePath,
                                ProcessedItems = processedActions,
                                TotalItems = totalActions,
                                BytesTransferred = totalNow,
                                TotalBytes = totalBytesToTransfer,
                                BytesPerSecond = speed
                            });
                        }, ct);

                        totalBytesTransferred += transferred;
                        copiedCount++;
                        item.Status = ItemSyncStatus.Completed;
                        break;
                    }

                    case SyncActionType.CopyRightToLeft:
                    {
                        var src = Path.Combine(plan.ResolvedRightPath, item.RelativePath);
                        var dst = Path.Combine(plan.ResolvedLeftPath, item.RelativePath);

                        var transferred = await CopyFileSafelyAsync(src, dst, job.UseSafeAtomicCopy, (bytesInFile) =>
                        {
                            var totalNow = totalBytesTransferred + bytesInFile;
                            var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                            var speed = elapsedSec > 0 ? totalNow / elapsedSec : 0;

                            progress?.Report(new SyncProgressReport
                            {
                                Phase = "Kopiowanie (Prawa ➡️ Lewa)...",
                                CurrentItemPath = item.RelativePath,
                                ProcessedItems = processedActions,
                                TotalItems = totalActions,
                                BytesTransferred = totalNow,
                                TotalBytes = totalBytesToTransfer,
                                BytesPerSecond = speed
                            });
                        }, ct);

                        totalBytesTransferred += transferred;
                        copiedCount++;
                        item.Status = ItemSyncStatus.Completed;
                        break;
                    }

                    case SyncActionType.DeleteRight:
                    {
                        var target = Path.Combine(plan.ResolvedRightPath, item.RelativePath);
                        if (File.Exists(target))
                        {
                            File.Delete(target);
                            deletedCount++;
                        }
                        item.Status = ItemSyncStatus.Completed;
                        break;
                    }

                    case SyncActionType.DeleteLeft:
                    {
                        var target = Path.Combine(plan.ResolvedLeftPath, item.RelativePath);
                        if (File.Exists(target))
                        {
                            File.Delete(target);
                            deletedCount++;
                        }
                        item.Status = ItemSyncStatus.Completed;
                        break;
                    }

                    case SyncActionType.Conflict:
                    case SyncActionType.Skipped:
                    default:
                        skippedCount++;
                        item.Status = ItemSyncStatus.Skipped;
                        break;
                }
            }
            catch (Exception ex)
            {
                errorsCount++;
                item.Status = ItemSyncStatus.Failed;
                item.ErrorMessage = ex.Message;
                errors.Add($"[{item.RelativePath}]: {ex.Message}");
            }
        }

        // 5. Update state snapshot
        await UpdateSnapshotAfterSyncAsync(job, plan, ct);

        stopwatch.Stop();

        // Update Job properties
        job.LastSyncTimeUtc = DateTime.UtcNow;
        job.LastSyncStatus = errorsCount == 0
            ? $"Ukończono pomyślnie ({copiedCount} skopiowano, {deletedCount} usunięto)"
            : $"Zakończono z błędami ({errorsCount} błędów, {copiedCount} skopiowano)";

        return new SyncResult
        {
            Success = errorsCount == 0,
            Duration = stopwatch.Elapsed,
            FilesCopied = copiedCount,
            FilesDeleted = deletedCount,
            FilesSkipped = skippedCount,
            ErrorsCount = errorsCount,
            TotalBytesTransferred = totalBytesTransferred,
            Errors = errors,
            ProcessedItems = plan.Items,
            SummaryMessage = job.LastSyncStatus
        };
    }

    private static async Task<long> CopyFileSafelyAsync(
        string sourcePath,
        string targetPath,
        bool useAtomic,
        Action<long> progressCallback,
        CancellationToken ct)
    {
        var srcInfo = new FileInfo(sourcePath);
        if (!srcInfo.Exists)
        {
            throw new FileNotFoundException($"Plik źródłowy nie istnieje: {sourcePath}");
        }

        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string tempTargetPath = useAtomic ? $"{targetPath}.synctmp_{Guid.NewGuid():N}" : targetPath;

        long fileTransferred = 0;
        byte[] buffer = new byte[CopyBufferSize];

        try
        {
            await using (var srcStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var dstStream = new FileStream(tempTargetPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.Asynchronous))
            {
                int read;
                while ((read = await srcStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await dstStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    fileTransferred += read;
                    progressCallback(fileTransferred);
                }
                await dstStream.FlushAsync(ct);
            }

            // Preserve timestamps and attributes
            File.SetCreationTimeUtc(tempTargetPath, srcInfo.CreationTimeUtc);
            File.SetLastWriteTimeUtc(tempTargetPath, srcInfo.LastWriteTimeUtc);

            if (useAtomic)
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(tempTargetPath, targetPath);
            }
        }
        catch
        {
            if (useAtomic && File.Exists(tempTargetPath))
            {
                try { File.Delete(tempTargetPath); } catch { }
            }
            throw;
        }

        return fileTransferred;
    }

    private async Task UpdateSnapshotAfterSyncAsync(SyncJob job, SyncPlan plan, CancellationToken ct)
    {
        try
        {
            var snapshot = new SyncStateSnapshot
            {
                JobId = job.Id,
                TimestampUtc = DateTime.UtcNow
            };

            foreach (var item in plan.Items)
            {
                if (item.Status == ItemSyncStatus.Failed)
                {
                    continue;
                }

                // If file was deleted on both sides, don't keep in snapshot
                if (item.Action is SyncActionType.DeleteRight or SyncActionType.DeleteLeft && item.Status == ItemSyncStatus.Completed)
                {
                    continue;
                }

                var sourceMeta = item.LeftFile ?? item.RightFile;
                if (sourceMeta == null) continue;

                snapshot.Entries[item.RelativePath] = new FileStateEntry
                {
                    RelativePath = item.RelativePath,
                    Size = sourceMeta.Size,
                    LastModifiedUtc = sourceMeta.LastModifiedUtc,
                    Hash = sourceMeta.Hash,
                    ExistedOnLeft = item.Action != SyncActionType.DeleteLeft,
                    ExistedOnRight = item.Action != SyncActionType.DeleteRight
                };
            }

            await _stateStore.SaveSnapshotAsync(snapshot, ct);
        }
        catch
        {
            // Snapshot saving failure shouldn't fail the sync
        }
    }
}

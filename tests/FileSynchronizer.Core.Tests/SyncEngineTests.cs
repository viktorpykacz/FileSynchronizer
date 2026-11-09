using System.IO;
using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Engine;
using FileSynchronizer.Core.Models;
using FileSynchronizer.Core.State;
using Xunit;

namespace FileSynchronizer.Core.Tests;

public class MockUsbDeviceWatcher : IUsbDeviceWatcher
{
    private readonly List<DriveDeviceInfo> _drives = new();

#pragma warning disable CS0067
    public event EventHandler<DriveDeviceInfo>? DriveConnected;
    public event EventHandler<string>? DriveDisconnected;
#pragma warning restore CS0067

    public MockUsbDeviceWatcher(IEnumerable<DriveDeviceInfo>? initialDrives = null)
    {
        if (initialDrives != null) _drives.AddRange(initialDrives);
    }

    public IReadOnlyList<DriveDeviceInfo> GetConnectedDrives() => _drives;
    public IReadOnlyList<DriveDeviceInfo> GetRemovableDrives() => _drives.Where(d => d.DriveType == DriveType.Removable).ToList();
    public void Start() { }
    public void Stop() { }
    public void Dispose() { }
}

public class SyncEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _leftDir;
    private readonly string _rightDir;
    private readonly string _snapshotsDir;
    private readonly JsonSyncStateStore _stateStore;
    private readonly MockUsbDeviceWatcher _mockUsbWatcher;
    private readonly SyncEngine _engine;

    public SyncEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FileSyncTests_" + Guid.NewGuid().ToString("N"));
        _leftDir = Path.Combine(_testDir, "Left");
        _rightDir = Path.Combine(_testDir, "Right");
        _snapshotsDir = Path.Combine(_testDir, "Snapshots");

        Directory.CreateDirectory(_leftDir);
        Directory.CreateDirectory(_rightDir);
        Directory.CreateDirectory(_snapshotsDir);

        _stateStore = new JsonSyncStateStore(_snapshotsDir);
        _mockUsbWatcher = new MockUsbDeviceWatcher();
        _engine = new SyncEngine(_mockUsbWatcher, _stateStore);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task OneWayBackup_NewFileOnLeft_CopiesToRight()
    {
        // Arrange
        var fileA = Path.Combine(_leftDir, "doc.txt");
        await File.WriteAllTextAsync(fileA, "Hello World from Left!");

        var job = new SyncJob
        {
            LeftPath = _leftDir,
            RightPath = _rightDir,
            Direction = SyncDirection.OneWayBackup
        };

        // Act - Analyze
        var plan = await _engine.AnalyzeAsync(job);

        // Assert Analysis
        Assert.Single(plan.Items);
        Assert.Equal(SyncActionType.CopyLeftToRight, plan.Items[0].Action);

        // Act - Execute
        var result = await _engine.ExecuteAsync(job, plan);

        // Assert Execution
        Assert.True(result.Success);
        Assert.Equal(1, result.FilesCopied);
        var targetFile = Path.Combine(_rightDir, "doc.txt");
        Assert.True(File.Exists(targetFile));
        Assert.Equal("Hello World from Left!", await File.ReadAllTextAsync(targetFile));
    }

    [Fact]
    public async Task OneWayBackup_DeleteOrphanFiles_DeletesFromRight()
    {
        // Arrange - File exists only on Right
        var orphan = Path.Combine(_rightDir, "old.txt");
        await File.WriteAllTextAsync(orphan, "Old orphan content");

        var job = new SyncJob
        {
            LeftPath = _leftDir,
            RightPath = _rightDir,
            Direction = SyncDirection.OneWayBackup,
            DeleteOrphanFilesOnTarget = true
        };

        // Act
        var plan = await _engine.AnalyzeAsync(job);
        Assert.Single(plan.Items);
        Assert.Equal(SyncActionType.DeleteRight, plan.Items[0].Action);

        var result = await _engine.ExecuteAsync(job, plan);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.FilesDeleted);
        Assert.False(File.Exists(orphan));
    }

    [Fact]
    public async Task TwoWaySync_PropagatesChangesBothWays()
    {
        // Arrange
        // Left has FileA, Right has FileB
        await File.WriteAllTextAsync(Path.Combine(_leftDir, "fileA.txt"), "Content A");
        await File.WriteAllTextAsync(Path.Combine(_rightDir, "fileB.txt"), "Content B");

        var job = new SyncJob
        {
            LeftPath = _leftDir,
            RightPath = _rightDir,
            Direction = SyncDirection.TwoWaySync
        };

        // Act 1: Analyze & Sync
        var plan = await _engine.AnalyzeAsync(job);
        Assert.Equal(2, plan.Items.Count);
        Assert.Contains(plan.Items, i => i.RelativePath == "fileA.txt" && i.Action == SyncActionType.CopyLeftToRight);
        Assert.Contains(plan.Items, i => i.RelativePath == "fileB.txt" && i.Action == SyncActionType.CopyRightToLeft);

        var result = await _engine.ExecuteAsync(job, plan);
        Assert.True(result.Success);
        Assert.Equal(2, result.FilesCopied);

        // Verify both sides now have both files
        Assert.True(File.Exists(Path.Combine(_leftDir, "fileB.txt")));
        Assert.True(File.Exists(Path.Combine(_rightDir, "fileA.txt")));

        // Act 2: Second run should find everything in sync
        var plan2 = await _engine.AnalyzeAsync(job);
        Assert.Equal(0, plan2.ActionRequiredCount);
        Assert.Equal(2, plan2.InSyncCount);
    }

    [Fact]
    public async Task TwoWaySync_DetectsConflictWhenBothModified()
    {
        // Initial setup: create file on both and establish baseline snapshot
        await File.WriteAllTextAsync(Path.Combine(_leftDir, "shared.txt"), "Base Content");
        await File.WriteAllTextAsync(Path.Combine(_rightDir, "shared.txt"), "Base Content");

        var job = new SyncJob
        {
            LeftPath = _leftDir,
            RightPath = _rightDir,
            Direction = SyncDirection.TwoWaySync,
            ConflictPolicy = ConflictResolutionPolicy.Manual
        };

        var initialPlan = await _engine.AnalyzeAsync(job);
        await _engine.ExecuteAsync(job, initialPlan);

        // Modify file on BOTH sides with different content & timestamps
        await Task.Delay(100);
        await File.WriteAllTextAsync(Path.Combine(_leftDir, "shared.txt"), "Left modified version");
        File.SetLastWriteTimeUtc(Path.Combine(_leftDir, "shared.txt"), DateTime.UtcNow.AddMinutes(5));

        await File.WriteAllTextAsync(Path.Combine(_rightDir, "shared.txt"), "Right modified version");
        File.SetLastWriteTimeUtc(Path.Combine(_rightDir, "shared.txt"), DateTime.UtcNow.AddMinutes(10));

        // Act: Analyze
        var conflictPlan = await _engine.AnalyzeAsync(job);

        // Assert: Conflict flagged
        Assert.True(conflictPlan.HasConflicts);
        var conflictItem = conflictPlan.Items.First(i => i.RelativePath == "shared.txt");
        Assert.Equal(SyncActionType.Conflict, conflictItem.Action);
    }
}

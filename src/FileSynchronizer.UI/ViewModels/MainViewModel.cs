using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Engine;
using FileSynchronizer.Core.Models;
using FileSynchronizer.Core.Services;
using FileSynchronizer.Core.State;
using FileSynchronizer.UI.Common;
using Microsoft.Win32;

namespace FileSynchronizer.UI.ViewModels;

public class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUsbDeviceWatcher _usbWatcher;
    private readonly ISyncStateStore _stateStore;
    private readonly IJobManager _jobManager;
    private readonly ISyncEngine _syncEngine;
    private readonly IUsbAutoSyncService _autoSyncService;

    private JobViewModel? _selectedJob;
    private SyncPlan? _currentPlan;
    private CancellationTokenSource? _cts;

    private bool _isBusy;
    private bool _isAnalyzing;
    private bool _isSyncing;
    private string _statusMessage = "Gotowy do pracy";
    private string _currentPhase = string.Empty;
    private string _currentItemText = string.Empty;
    private double _progressPercentage;
    private string _speedText = string.Empty;
    private string _progressCountText = string.Empty;

    public ObservableCollection<JobViewModel> Jobs { get; } = new();
    public ObservableCollection<DriveItemViewModel> AvailableDrives { get; } = new();
    public ObservableCollection<SyncItemViewModel> PlanItems { get; } = new();

    public JobViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                ClearPlan();
                _selectedJob?.RefreshDisplays(_usbWatcher.GetConnectedDrives());
                OnPropertyChanged(nameof(HasSelectedJob));
                OnPropertyChanged(nameof(CanAnalyze));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public event EventHandler<(string Title, string Message, bool IsError)>? NotificationRequested;

    public bool HasSelectedJob => SelectedJob != null;
    public bool IsBusy => _isBusy;
    public bool IsAnalyzing => _isAnalyzing;
    public bool IsSyncing => _isSyncing;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentPhase
    {
        get => _currentPhase;
        set => SetProperty(ref _currentPhase, value);
    }

    public string CurrentItemText
    {
        get => _currentItemText;
        set => SetProperty(ref _currentItemText, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string SpeedText
    {
        get => _speedText;
        set => SetProperty(ref _speedText, value);
    }

    public string ProgressCountText
    {
        get => _progressCountText;
        set => SetProperty(ref _progressCountText, value);
    }

    // Summary Metrics for current plan
    public int PlanTotalCount => PlanItems.Count;
    public int PlanActionsCount => PlanItems.Count(i => i.RequiresAction);
    public int PlanConflictsCount => PlanItems.Count(i => i.IsConflict);
    public int PlanInSyncCount => PlanItems.Count(i => i.Action == SyncActionType.InSync);
    public string PlanSizeFormatted => FormatBytes(_currentPlan?.TotalBytesToTransfer ?? 0);
    public bool HasPlan => _currentPlan != null && PlanItems.Count > 0;
    public bool CanSync => HasPlan && PlanActionsCount > 0 && !_isBusy;
    public bool CanAnalyze => HasSelectedJob && !_isBusy;

    public ICommand AddJobCommand { get; }
    public ICommand DeleteJobCommand { get; }
    public ICommand SaveJobCommand { get; }
    public ICommand BrowseLeftFolderCommand { get; }
    public ICommand BrowseRightFolderCommand { get; }
    public ICommand SelectUsbForRightCommand { get; }
    public ICommand RefreshDrivesCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand SyncCommand { get; }
    public ICommand CancelCommand { get; }

    public MainViewModel()
    {
        _usbWatcher = new UsbDeviceWatcher();
        _stateStore = new JsonSyncStateStore();
        _jobManager = new JobManager();
        _syncEngine = new SyncEngine(_usbWatcher, _stateStore);
        _autoSyncService = new UsbAutoSyncService(_usbWatcher, _jobManager, _syncEngine);

        AddJobCommand = new RelayCommand(AddNewJob);
        DeleteJobCommand = new RelayCommand(DeleteSelectedJob, () => HasSelectedJob && !_isBusy);
        SaveJobCommand = new AsyncRelayCommand(SaveCurrentJobAsync);
        BrowseLeftFolderCommand = new RelayCommand(BrowseLeftFolder, () => HasSelectedJob && !_isBusy);
        BrowseRightFolderCommand = new RelayCommand(BrowseRightFolder, () => HasSelectedJob && !_isBusy);
        SelectUsbForRightCommand = new RelayCommand(SelectUsbDriveAsRight, _ => HasSelectedJob && !_isBusy);
        RefreshDrivesCommand = new RelayCommand(RefreshConnectedDrives);

        AnalyzeCommand = new AsyncRelayCommand(AnalyzeSelectedJobAsync, () => CanAnalyze);
        SyncCommand = new AsyncRelayCommand(ExecuteSyncAsync, () => CanSync);
        CancelCommand = new RelayCommand(CancelOperation, () => _isBusy);

        // Setup USB events
        _usbWatcher.DriveConnected += (s, d) => RunOnUiThread(() =>
        {
            RefreshConnectedDrives();
            StatusMessage = $"🟢 Podłączono nośnik USB: {d.DisplayName}";
        });

        _usbWatcher.DriveDisconnected += (s, root) => RunOnUiThread(() =>
        {
            RefreshConnectedDrives();
            StatusMessage = $"🔴 Odłączono nośnik: {root}";
        });

        _autoSyncService.AutoSyncStarted += (s, e) => RunOnUiThread(() =>
        {
            StatusMessage = $"⚡ [Auto-Sync] Rozpoczęto synchronizację dla '{e.Job.Name}'...";
            NotificationRequested?.Invoke(this, ("⚡ Auto-Sync rozpoczęty", $"Wykryto USB. Rozpoczynanie synchronizacji dla '{e.Job.Name}'...", false));
        });

        _autoSyncService.AutoSyncCompleted += (s, e) => RunOnUiThread(() =>
        {
            StatusMessage = $"✔️ [Auto-Sync] Ukończono '{e.Job.Name}' (Skopiowano: {e.Result?.FilesCopied})";
            NotificationRequested?.Invoke(this, ("✔️ Auto-Sync zakończony sukcesem", $"Zadanie '{e.Job.Name}' ukończone. Skopiowano: {e.Result?.FilesCopied} plików.", false));
            if (SelectedJob?.Id == e.Job.Id)
            {
                SelectedJob.LastSyncTimeUtc = e.Job.LastSyncTimeUtc;
                SelectedJob.LastSyncStatus = e.Job.LastSyncStatus ?? "Ukończono pomyślnie";
            }
        });

        _autoSyncService.AutoSyncFailed += (s, e) => RunOnUiThread(() =>
        {
            StatusMessage = $"❌ [Auto-Sync Błąd] '{e.Job.Name}': {e.ErrorMessage}";
            NotificationRequested?.Invoke(this, ("❌ Auto-Sync Błąd", $"Zadanie '{e.Job.Name}' nie powiodło się: {e.ErrorMessage}", true));
        });

        _autoSyncService.Start();
        RefreshConnectedDrives();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _jobManager.LoadJobsAsync();
        var jobsList = _jobManager.GetAllJobs();

        RunOnUiThread(() =>
        {
            Jobs.Clear();
            foreach (var job in jobsList)
            {
                Jobs.Add(new JobViewModel(job));
            }

            if (Jobs.Count == 0)
            {
                // Create a default starter job
                AddNewJob();
            }
            else
            {
                SelectedJob = Jobs[0];
            }
        });
    }

    public void RefreshConnectedDrives()
    {
        RunOnUiThread(() =>
        {
            AvailableDrives.Clear();
            var drives = _usbWatcher.GetConnectedDrives();
            foreach (var drive in drives)
            {
                AvailableDrives.Add(new DriveItemViewModel(drive));
            }
        });
    }

    private void AddNewJob()
    {
        var newJob = new SyncJob
        {
            Name = $"Zadanie {Jobs.Count + 1}",
            Direction = SyncDirection.OneWayBackup,
            AutoSyncOnUsbConnect = false
        };

        var vm = new JobViewModel(newJob);
        Jobs.Add(vm);
        SelectedJob = vm;
        _ = _jobManager.AddJobAsync(newJob);
    }

    private void DeleteSelectedJob()
    {
        if (SelectedJob == null) return;

        var result = MessageBox.Show(
            $"Czy na pewno chcesz usunąć zadanie '{SelectedJob.Name}'?",
            "Usuwanie zadania",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var jobToDelete = SelectedJob;
            var idx = Jobs.IndexOf(jobToDelete);
            Jobs.Remove(jobToDelete);
            _ = _jobManager.DeleteJobAsync(jobToDelete.Id);

            SelectedJob = Jobs.Count > 0 ? Jobs[Math.Max(0, idx - 1)] : null;
        }
    }

    public async Task SaveCurrentJobAsync()
    {
        if (SelectedJob != null)
        {
            await _jobManager.UpdateJobAsync(SelectedJob.Model);
        }
    }

    private void BrowseLeftFolder()
    {
        if (SelectedJob == null) return;

        var connectedDrives = _usbWatcher.GetConnectedDrives();
        string initialDir = "";
        if (!string.IsNullOrWhiteSpace(SelectedJob.LeftPath))
        {
            var (ok, resolved, _) = UsbPathResolver.ResolvePath(SelectedJob.LeftPath, connectedDrives);
            if (ok && Directory.Exists(resolved)) initialDir = resolved;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder źródłowy (Left)",
            InitialDirectory = initialDir
        };

        if (dialog.ShowDialog() == true)
        {
            var (isUsb, usbUri, matchedDrive) = UsbPathResolver.TryConvertPathToUsbUri(dialog.FolderName, connectedDrives);
            if (isUsb && matchedDrive != null)
            {
                SelectedJob.LeftPath = usbUri;
                StatusMessage = $"Wybrano folder na pendrive: [{matchedDrive.DisplayName}]";
            }
            else
            {
                SelectedJob.LeftPath = dialog.FolderName;
            }

            _ = SaveCurrentJobAsync();
            ClearPlan();
            SelectedJob.RefreshDisplays(connectedDrives);
        }
    }

    private void BrowseRightFolder()
    {
        if (SelectedJob == null) return;

        var connectedDrives = _usbWatcher.GetConnectedDrives();
        string initialDir = "";
        if (!string.IsNullOrWhiteSpace(SelectedJob.RightPath))
        {
            var (ok, resolved, _) = UsbPathResolver.ResolvePath(SelectedJob.RightPath, connectedDrives);
            if (ok && Directory.Exists(resolved)) initialDir = resolved;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder docelowy (Right)",
            InitialDirectory = initialDir
        };

        if (dialog.ShowDialog() == true)
        {
            var (isUsb, usbUri, matchedDrive) = UsbPathResolver.TryConvertPathToUsbUri(dialog.FolderName, connectedDrives);
            if (isUsb && matchedDrive != null)
            {
                SelectedJob.RightPath = usbUri;
                SelectedJob.TargetUsbVolumeSerial = matchedDrive.VolumeSerialNumber;
                SelectedJob.TargetUsbVolumeLabel = matchedDrive.VolumeName;
                StatusMessage = $"Wybrano folder na pendrive: [{matchedDrive.DisplayName}]";
            }
            else
            {
                SelectedJob.RightPath = dialog.FolderName;
                SelectedJob.TargetUsbVolumeSerial = null;
                SelectedJob.TargetUsbVolumeLabel = null;
            }

            _ = SaveCurrentJobAsync();
            ClearPlan();
            SelectedJob.RefreshDisplays(connectedDrives);
        }
    }

    private void SelectUsbDriveAsRight(object? parameter)
    {
        if (SelectedJob == null || parameter is not DriveItemViewModel driveVm) return;

        SelectedJob.RightPath = driveVm.DynamicUsbUri;
        SelectedJob.TargetUsbVolumeSerial = driveVm.VolumeSerialNumber;
        SelectedJob.TargetUsbVolumeLabel = driveVm.VolumeName;
        SelectedJob.AutoSyncOnUsbConnect = true;

        _ = SaveCurrentJobAsync();
        ClearPlan();
        SelectedJob.RefreshDisplays(_usbWatcher.GetConnectedDrives());
        StatusMessage = $"Przypisano pendrive '{driveVm.DisplayName}' jako folder docelowy.";
    }

    private void ClearPlan()
    {
        _currentPlan = null;
        PlanItems.Clear();
        RefreshPlanMetrics();
    }

    private void RefreshPlanMetrics()
    {
        OnPropertyChanged(nameof(PlanTotalCount));
        OnPropertyChanged(nameof(PlanActionsCount));
        OnPropertyChanged(nameof(PlanConflictsCount));
        OnPropertyChanged(nameof(PlanInSyncCount));
        OnPropertyChanged(nameof(PlanSizeFormatted));
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(CanSync));
        OnPropertyChanged(nameof(CanAnalyze));
    }

    private async Task AnalyzeSelectedJobAsync()
    {
        if (SelectedJob == null) return;

        SetBusy(true, isAnalyzing: true);
        StatusMessage = "Analizowanie zawartości folderów...";
        CurrentPhase = "Przygotowywanie analizy...";
        ProgressPercentage = 0;
        SpeedText = string.Empty;
        ClearPlan();

        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<SyncProgressReport>(report =>
            {
                RunOnUiThread(() =>
                {
                    CurrentPhase = report.Phase;
                    CurrentItemText = report.CurrentItemPath ?? "";
                    if (report.TotalItems > 0)
                    {
                        ProgressPercentage = report.ItemPercentage;
                        ProgressCountText = $"{report.ProcessedItems} / {report.TotalItems}";
                    }
                });
            });

            var plan = await _syncEngine.AnalyzeAsync(SelectedJob.Model, progress, _cts.Token);
            _currentPlan = plan;

            RunOnUiThread(() =>
            {
                PlanItems.Clear();
                foreach (var item in plan.Items)
                {
                    PlanItems.Add(new SyncItemViewModel(item));
                }

                RefreshPlanMetrics();
                ProgressPercentage = 100;
                CurrentPhase = "Analiza zakończona!";
                CurrentItemText = string.Empty;

                if (plan.ActionRequiredCount == 0)
                {
                    StatusMessage = "✨ Foldery są w 100% zsynchronizowane. Brak wymaganych akcji.";
                }
                else
                {
                    StatusMessage = $"🔍 Znaleziono {plan.ActionRequiredCount} plików do zsynchronizowania ({plan.ConflictCount} konfliktów).";
                }
            });
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Analiza została anulowana przez użytkownika.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Błąd podczas analizy: {ex.Message}";
            MessageBox.Show(ex.Message, "Błąd analizy", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExecuteSyncAsync()
    {
        if (SelectedJob == null || _currentPlan == null) return;

        SetBusy(true, isSyncing: true);
        StatusMessage = "Trwa synchronizacja plików...";
        CurrentPhase = "Inicjalizacja kopiowania...";
        ProgressPercentage = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<SyncProgressReport>(report =>
            {
                RunOnUiThread(() =>
                {
                    CurrentPhase = report.Phase;
                    CurrentItemText = report.CurrentItemPath ?? "";
                    ProgressPercentage = report.BytesPercentage > 0 ? report.BytesPercentage : report.ItemPercentage;
                    ProgressCountText = $"{report.ProcessedItems} / {report.TotalItems} plików";

                    var speedMb = report.BytesPerSecond / (1024 * 1024);
                    SpeedText = speedMb > 0 ? $"{speedMb:0.0} MB/s" : "";
                });
            });

            var result = await _syncEngine.ExecuteAsync(SelectedJob.Model, _currentPlan, progress, _cts.Token);

            RunOnUiThread(() =>
            {
                foreach (var vm in PlanItems)
                {
                    vm.RefreshState();
                }

                if (SelectedJob != null)
                {
                    SelectedJob.LastSyncTimeUtc = SelectedJob.Model.LastSyncTimeUtc;
                    SelectedJob.LastSyncStatus = SelectedJob.Model.LastSyncStatus ?? "Ukończono pomyślnie";
                    _ = SaveCurrentJobAsync();
                }

                ProgressPercentage = 100;
                CurrentPhase = "Synchronizacja ukończona!";
                CurrentItemText = string.Empty;
                SpeedText = string.Empty;

                if (result.Success)
                {
                    StatusMessage = $"🎉 Synchronizacja zakończona pomyślnie! Skopiowano {result.FilesCopied} plików w {result.Duration.TotalSeconds:0.0}s.";
                }
                else
                {
                    StatusMessage = $"⚠️ Zakończono z {result.ErrorsCount} błędami. Sprawdź szczegóły w tabeli.";
                }

                RefreshPlanMetrics();
            });
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Synchronizacja została przerwana przez użytkownika.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Błąd synchronizacji: {ex.Message}";
            MessageBox.Show(ex.Message, "Błąd synchronizacji", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void CancelOperation()
    {
        _cts?.Cancel();
        StatusMessage = "Trwa zatrzymywanie operacji...";
    }

    private void SetBusy(bool busy, bool isAnalyzing = false, bool isSyncing = false)
    {
        _isBusy = busy;
        _isAnalyzing = isAnalyzing;
        _isSyncing = isSyncing;

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsAnalyzing));
        OnPropertyChanged(nameof(IsSyncing));
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(CanSync));
        CommandManager.InvalidateRequerySuggested();
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    public void Dispose()
    {
        _autoSyncService.Dispose();
        _usbWatcher.Dispose();
        _cts?.Dispose();
    }
}

using FileSynchronizer.Core.Models;
using FileSynchronizer.UI.Common;

namespace FileSynchronizer.UI.ViewModels;

public class SyncItemViewModel : ObservableObject
{
    public SyncItem Model { get; }

    public string RelativePath => Model.RelativePath;
    public string FileName => System.IO.Path.GetFileName(Model.RelativePath);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Model.RelativePath)?.Replace('\\', '/') ?? "";

    public bool LeftExists => Model.LeftFile?.Exists == true;
    public long LeftSize => Model.LeftFile?.Size ?? 0;
    public DateTime? LeftModifiedUtc => Model.LeftFile?.LastModifiedUtc.ToLocalTime();
    public string LeftModifiedFormatted => LeftModifiedUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public bool RightExists => Model.RightFile?.Exists == true;
    public long RightSize => Model.RightFile?.Size ?? 0;
    public DateTime? RightModifiedUtc => Model.RightFile?.LastModifiedUtc.ToLocalTime();
    public string RightModifiedFormatted => RightModifiedUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public SyncActionType Action
    {
        get => Model.Action;
        set
        {
            if (Model.Action != value)
            {
                Model.Action = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActionIcon));
                OnPropertyChanged(nameof(ActionText));
                OnPropertyChanged(nameof(RequiresAction));
            }
        }
    }

    public ItemSyncStatus Status
    {
        get => Model.Status;
        set
        {
            if (Model.Status != value)
            {
                Model.Status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string ActionIcon => Model.Action switch
    {
        SyncActionType.CopyLeftToRight => "➡️",
        SyncActionType.CopyRightToLeft => "⬅️",
        SyncActionType.DeleteRight => "❌",
        SyncActionType.DeleteLeft => "❌",
        SyncActionType.Conflict => "⚠️",
        SyncActionType.InSync => "✔️",
        SyncActionType.Skipped => "⏭️",
        _ => "❓"
    };

    public string ActionText => Model.Action switch
    {
        SyncActionType.CopyLeftToRight => "Kopiuj ➡️",
        SyncActionType.CopyRightToLeft => "⬅️ Kopiuj",
        SyncActionType.DeleteRight => "Usuń z celu ❌",
        SyncActionType.DeleteLeft => "Usuń ze źródła ❌",
        SyncActionType.Conflict => "KONFLIKT ⚠️",
        SyncActionType.InSync => "Identyczny ✔️",
        SyncActionType.Skipped => "Pominięty",
        _ => "Błąd"
    };

    public string StatusIcon => Model.Status switch
    {
        ItemSyncStatus.Completed => "✅",
        ItemSyncStatus.InProgress => "⏳",
        ItemSyncStatus.Failed => "❌",
        ItemSyncStatus.Skipped => "⏭️",
        _ => "⚪"
    };

    public string StatusText => Model.ErrorMessage ?? Model.StatusDescription ?? Model.Status.ToString();
    public bool RequiresAction => Model.RequiresAction;
    public bool IsConflict => Model.IsConflict;

    public SyncItemViewModel(SyncItem model)
    {
        Model = model;
    }

    public void RefreshState()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusText));
    }
}

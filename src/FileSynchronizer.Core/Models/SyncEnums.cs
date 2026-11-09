namespace FileSynchronizer.Core.Models;

/// <summary>
/// Direction of synchronization.
/// </summary>
public enum SyncDirection
{
    /// <summary>
    /// Source (Left) to Destination (Right).
    /// Target is updated to match Source.
    /// </summary>
    OneWayBackup,

    /// <summary>
    /// Bi-directional synchronization between Left and Right.
    /// Changes from both sides are propagated, conflicts are flagged.
    /// </summary>
    TwoWaySync
}

/// <summary>
/// Action to be taken for an individual file during synchronization.
/// </summary>
public enum SyncActionType
{
    /// <summary>
    /// File is in sync on both sides, no action needed.
    /// </summary>
    InSync,

    /// <summary>
    /// Copy new or modified file from Left to Right (➡️).
    /// </summary>
    CopyLeftToRight,

    /// <summary>
    /// Copy new or modified file from Right to Left (⬅️).
    /// </summary>
    CopyRightToLeft,

    /// <summary>
    /// Delete file on Right (❌ right).
    /// </summary>
    DeleteRight,

    /// <summary>
    /// Delete file on Left (❌ left).
    /// </summary>
    DeleteLeft,

    /// <summary>
    /// File modified on both sides since last sync (⚠️ conflict).
    /// </summary>
    Conflict,

    /// <summary>
    /// File is ignored due to exclusion rules.
    /// </summary>
    Skipped,

    /// <summary>
    /// Error during analysis.
    /// </summary>
    Error
}

/// <summary>
/// Execution status of an individual sync item.
/// </summary>
public enum ItemSyncStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// Method used to determine if two files are different.
/// </summary>
public enum CompareMethod
{
    /// <summary>
    /// Compare file size and last modified timestamp (fastest, default).
    /// </summary>
    SizeAndDate,

    /// <summary>
    /// Compare size and date first, if different or uncertain calculate xxHash64 hash.
    /// </summary>
    SizeAndDateWithHashFallback,

    /// <summary>
    /// Always compute and compare xxHash64 checksums.
    /// </summary>
    HashOnly
}

/// <summary>
/// Policy for automatically resolving conflicts.
/// </summary>
public enum ConflictResolutionPolicy
{
    /// <summary>
    /// Do not copy, flag as conflict for manual user decision.
    /// </summary>
    Manual,

    /// <summary>
    /// Left (Source) always wins.
    /// </summary>
    LeftWins,

    /// <summary>
    /// Right (Destination) always wins.
    /// </summary>
    RightWins,

    /// <summary>
    /// The newer file (by LastWriteTime) wins.
    /// </summary>
    NewerWins,

    /// <summary>
    /// Rename both files with timestamp suffix to keep both versions.
    /// </summary>
    KeepBoth
}

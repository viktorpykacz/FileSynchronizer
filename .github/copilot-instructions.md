# GitHub Copilot Instructions for FileSynchronizer

You are an expert .NET 9 and WPF engineer assisting with the **FileSynchronizer** project.

## Key Principles & Architectural Patterns:
1. **Target Framework**: .NET 9 (`net9.0-windows`), C# 13 with nullable reference types enabled.
2. **Core vs UI**:
   - `FileSynchronizer.Core` contains domain models, sync engine (`SyncEngine`), file scanner, hashing (`System.IO.Hashing.XxHash64`), snapshot state (`JsonSyncStateStore`), and USB watcher (`UsbDeviceWatcher`). Keep `Core` free of WPF dependencies.
   - `FileSynchronizer.UI` contains WPF views and MVVM viewmodels (`MainViewModel`, `JobViewModel`, `SyncItemViewModel`, `DriveItemViewModel`).
3. **Synchronization Safety**:
   - Always copy files through a temporary file (`.tmp`) before moving to destination with `File.Move(tempFile, destination, overwrite: true)`.
   - Preserve timestamps (`CreationTimeUtc`, `LastWriteTimeUtc`).
   - Honor glob exclude patterns (e.g., `*.tmp`, `Thumbs.db`, `.git/*`).
4. **USB Drive Detection**:
   - USB storage devices are matched by `VolumeSerialNumber` or `VolumeLabel` via `UsbPathResolver` to be resilient to drive letter reassignments.
5. **Code Style**:
   - Prefer file-scoped namespaces.
   - Use async/await throughout I/O operations and respect `CancellationToken`.
   - Ensure progress reporting uses `IProgress<T>`.
   - Thread safety: marshal UI updates through `Application.Current.Dispatcher` when modifying observable collections or bound properties from background tasks.
6. **Tests**:
   - Written in xUnit (`tests/FileSynchronizer.Core.Tests`).
   - Run tests using `dotnet test`. Always ensure new logic includes unit tests covering edge cases.

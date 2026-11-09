# CLAUDE.md - FileSynchronizer Instructions

## Project Context
- **Name**: FileSynchronizer
- **Type**: .NET 9 Desktop Application (WPF UI, Core Engine, CLI runner, xUnit tests)
- **Purpose**: GoodSync-like two-phase synchronization utility (Analyze -> Sync) with USB auto-detection and persistence.

## Common Commands
- Build: `dotnet build`
- Run Tests: `dotnet test`
- Run UI: `dotnet run --project src/FileSynchronizer.UI`
- Run CLI: `dotnet run --project src/FileSynchronizer.Cli`

## Architecture & Conventions
- **Language**: C# 13, .NET 9 (`net9.0-windows`).
- **File-scoped namespaces**: `namespace FileSynchronizer.<SubNamespace>;`
- **Nullable Reference Types**: Enabled. Avoid null suppression unless strictly necessary.
- **Async & Cancellation**: Use `CancellationToken` and `IProgress<T>` for all long-running I/O or sync routines.
- **File Operations**: Must be atomic (write to temp file, rename) and preserve `CreationTimeUtc` and `LastWriteTimeUtc`.
- **Hashing**: Use `System.IO.Hashing.XxHash64` for file checksum calculation.
- **UI Architecture**: MVVM pattern with `ObservableObject` and `RelayCommand`. UI changes from background threads must be dispatched via `Application.Current.Dispatcher`.
- **Tests**: Located in `tests/FileSynchronizer.Core.Tests`. Must use temporary test directories and clean up properly.

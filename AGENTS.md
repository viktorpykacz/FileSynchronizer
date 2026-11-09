# AI Agent Guidelines - FileSynchronizer

This document provides context, architectural guidelines, and conventions for AI assistants working on the **FileSynchronizer** repository.

---

## 📌 Project Overview

**FileSynchronizer** is a GoodSync-inspired file synchronization and backup desktop utility built with **C# .NET 9**. It specializes in synchronization between local drives and USB removable storage with automated USB volume tracking.

### Key Capabilities:
- **Two-phase synchronization**: 
  1. *Analyze phase* (Dry-run / Diffing against snapshot database with status tagging: Copy, Delete, Conflict, Unchanged).
  2. *Sync phase* (Execution with atomic streaming, temp-file swaps, and timestamp preservation).
- **Intelligent USB drive tracking**: Drives identified by **Volume Serial Number** or **Volume Label** so changing drive letters do not break sync jobs.
- **Background Auto-Sync on USB plug-in**: Listens to Windows device arrival notifications (WMI + Win32 API).
- **xxHash64 hashing**: High-throughput file comparison via `System.IO.Hashing`.
- **WPF Dark Modern UI**: Split-view comparison grid, real-time transfer speed (MB/s), tray minimization.

---

## 🏗️ Architecture & Project Structure

```
FileSynchronizer/
├── FileSynchronizer.sln
├── src/
│   ├── FileSynchronizer.Core/          # Domain models, sync engine, state persistence, USB watcher
│   │   ├── Devices/                    # UsbDeviceWatcher, UsbPathResolver
│   │   ├── Engine/                     # SyncEngine, FileScanner, FileHasher, PatternMatcher
│   │   ├── Models/                     # SyncJob, SyncItem, SyncPlan, SyncResult, FileMetadata, Enums
│   │   ├── Services/                   # JobManager, UsbAutoSyncService
│   │   └── State/                      # JsonSyncStateStore, SyncStateSnapshot
│   ├── FileSynchronizer.UI/            # WPF desktop application (MVVM, Dark UI)
│   │   ├── Common/                     # ObservableObject, RelayCommand, Converters
│   │   ├── Services/                   # TrayIconService
│   │   └── ViewModels/                 # MainViewModel, JobViewModel, SyncItemViewModel, DriveItemViewModel
│   └── FileSynchronizer.Cli/           # Command-line runner / headless execution
└── tests/
    └── FileSynchronizer.Core.Tests/    # xUnit test suite
```

---

## 🛠️ Build, Run & Test Commands

Run from repository root:

- **Build solution**:
  ```powershell
  dotnet build
  ```
- **Run all unit tests**:
  ```powershell
  dotnet test
  ```
- **Run desktop app (WPF)**:
  ```powershell
  dotnet run --project src/FileSynchronizer.UI
  ```
- **Run CLI tool**:
  ```powershell
  dotnet run --project src/FileSynchronizer.Cli
  ```

---

## 💻 Code Style & Conventions

- **Language / Framework**: C# 13, .NET 9, Windows Desktop (`net9.0-windows`).
- **Namespaces**: Use file-scoped namespaces (`namespace FileSynchronizer.Core.Engine;`).
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Do not use suppression operators (`!`) without explicit justification.
- **Async Pattern**: 
  - Always accept and pass `CancellationToken cancellationToken = default`.
  - Report progress using `IProgress<T>` for long-running operations.
  - Configure awaits appropriately for UI synchronization context.
- **Safety & Atomic Operations**:
  - All file copying must use atomic techniques (write to temporary `.tmp` file first, then `File.Move(..., overwrite: true)`).
  - Preserve file timestamps: `File.SetCreationTimeUtc` and `File.SetLastWriteTimeUtc`.
- **MVVM Pattern in UI**:
  - Keep code-behind minimal; drive UI through ViewModel data-binding and commands (`RelayCommand`).
  - Implement properties using `ObservableObject.SetField(ref _field, value)`.
  - Dispatch UI updates to `Application.Current.Dispatcher` when invoked from background threads.
- **Testing**:
  - Unit tests use **xUnit**.
  - All file system tests must use isolated temporary directories and clean up afterwards via `IDisposable`.

using System.Text;
using FileSynchronizer.Core.Devices;
using FileSynchronizer.Core.Engine;
using FileSynchronizer.Core.Models;
using FileSynchronizer.Core.Services;
using FileSynchronizer.Core.State;

Console.OutputEncoding = Encoding.UTF8;
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=================================================");
Console.WriteLine("   🚀 FileSynchronizer Core & USB Engine CLI    ");
Console.WriteLine("   (Klon mechanizmu GoodSync w technologii .NET)  ");
Console.WriteLine("=================================================\n");
Console.ResetColor();

using var usbWatcher = new UsbDeviceWatcher();
var stateStore = new JsonSyncStateStore();
var jobManager = new JobManager();
await jobManager.LoadJobsAsync();
var syncEngine = new SyncEngine(usbWatcher, stateStore);
using var autoSyncService = new UsbAutoSyncService(usbWatcher, jobManager, syncEngine);

// Start USB Auto-Sync in background
autoSyncService.AutoSyncStarted += (s, e) =>
{
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"\n[⚡ AUTO-SYNC] Wykryto USB '{e.Drive.DisplayName}'! Rozpoczynanie synchronizacji dla zadania: '{e.Job.Name}'...");
    Console.ResetColor();
};

autoSyncService.AutoSyncCompleted += (s, e) =>
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[✔️ AUTO-SYNC SUKCES] Zadanie '{e.Job.Name}' zakończone. Skopiowano: {e.Result?.FilesCopied}, Usunięto: {e.Result?.FilesDeleted}");
    Console.ResetColor();
};

autoSyncService.AutoSyncFailed += (s, e) =>
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[❌ AUTO-SYNC BŁĄD] Zadanie '{e.Job.Name}' nie powiodło się: {e.ErrorMessage}");
    Console.ResetColor();
};

autoSyncService.Start();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\nWybierz opcję:");
    Console.ResetColor();
    Console.WriteLine(" 1. 💾 Wyświetl wykryte dyski i nośniki USB");
    Console.WriteLine(" 2. 📋 Wyświetl zapisane zadania synchronizacji (Jobs)");
    Console.WriteLine(" 3. ➕ Utwórz nowe zadanie synchronizacji");
    Console.WriteLine(" 4. 🔍 Analizuj i Uruchom zadanie (Tryb GoodSync)");
    Console.WriteLine(" 5. 🧪 Uruchom szybki test demo w folderze tymczasowym");
    Console.WriteLine(" 6. 🎧 Nasłuchuj podłączeń USB w czasie rzeczywistym");
    Console.WriteLine(" 0. 🚪 Wyjście");
    Console.Write("\nTwój wybór > ");

    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    switch (choice)
    {
        case "1":
            ShowConnectedDrives(usbWatcher);
            break;
        case "2":
            ShowJobs(jobManager);
            break;
        case "3":
            await CreateNewJobInteractiveAsync(jobManager, usbWatcher);
            break;
        case "4":
            await RunJobInteractiveAsync(jobManager, syncEngine);
            break;
        case "5":
            await RunDemoTestAsync(syncEngine, usbWatcher);
            break;
        case "6":
            ListenUsbEvents(usbWatcher);
            break;
        case "0":
            Console.WriteLine("Zamykanie programu...");
            return;
        default:
            Console.WriteLine("Nieznana opcja, spróbuj ponownie.");
            break;
    }
}

static void ShowConnectedDrives(IUsbDeviceWatcher watcher)
{
    var drives = watcher.GetConnectedDrives();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"--- Wykryte dyski ({drives.Count}) ---");
    Console.ResetColor();

    foreach (var d in drives)
    {
        var typeTag = d.DriveType == DriveType.Removable ? "[USB/Removable]" : $"[{d.DriveType}]";
        Console.WriteLine($"\n • {typeTag} {d.DisplayName}");
        Console.WriteLine($"   Litera/Root: {d.RootPath}");
        Console.WriteLine($"   Volume Serial: {d.VolumeSerialNumber}");
        Console.WriteLine($"   Format: {d.DriveFormat}");
        Console.WriteLine($"   Dynamiczny URI: {UsbPathResolver.CreateUsbUri(d)}");
    }
}

static void ShowJobs(IJobManager jobManager)
{
    var jobs = jobManager.GetAllJobs();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"--- Zapisane zadania ({jobs.Count}) ---");
    Console.ResetColor();

    if (jobs.Count == 0)
    {
        Console.WriteLine("Brak zdefiniowanych zadań. Wybierz opcję 3, aby utworzyć.");
        return;
    }

    int idx = 1;
    foreach (var j in jobs)
    {
        var dirStr = j.Direction == SyncDirection.OneWayBackup ? "Kopia 1-kierunkowa (➡️)" : "Synchronizacja 2-kierunkowa (🔄)";
        Console.WriteLine($"\n[{idx++}] {j.Name} ({dirStr})");
        Console.WriteLine($"    Źródło (Left):   {j.LeftPath}");
        Console.WriteLine($"    Cel (Right):     {j.RightPath}");
        Console.WriteLine($"    Auto-Sync USB:   {(j.AutoSyncOnUsbConnect ? "TAK [Włączone]" : "NIE")}");
        Console.WriteLine($"    Ostatni status:  {j.LastSyncStatus ?? "Jeszcze nie uruchamiano"}");
    }
}

static async Task CreateNewJobInteractiveAsync(IJobManager jobManager, IUsbDeviceWatcher usbWatcher)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("--- Tworzenie nowego zadania ---");
    Console.ResetColor();

    Console.Write("Nazwa zadania (np. Kopia na Pendrive): ");
    var name = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(name)) name = "Kopia zapasowa";

    Console.Write("Ścieżka źródłowa (Left, np. C:\\MojeDokumenty): ");
    var left = Console.ReadLine()?.Trim() ?? "";

    Console.WriteLine("\nCzy celem (Right) jest pendrive USB?");
    Console.WriteLine(" 1. Tak, wybierz z aktualnie podłączonych pendrive'ów");
    Console.WriteLine(" 2. Nie, wpiszę standardową ścieżkę");
    Console.Write("Wybór > ");
    var usbChoice = Console.ReadLine()?.Trim();

    string rightPath;
    string? usbSerial = null;
    string? usbLabel = null;
    bool autoSync = false;

    if (usbChoice == "1")
    {
        var drives = usbWatcher.GetConnectedDrives();
        for (int i = 0; i < drives.Count; i++)
        {
            Console.WriteLine($" [{i + 1}] {drives[i].DisplayName} (Serial: {drives[i].VolumeSerialNumber})");
        }
        Console.Write("Wybierz numer dysku: ");
        if (int.TryParse(Console.ReadLine(), out int dIdx) && dIdx >= 1 && dIdx <= drives.Count)
        {
            var selectedDrive = drives[dIdx - 1];
            Console.Write("Podkatalog na pendrive (opcjonalnie, np. Backup/Dokumenty): ");
            var sub = Console.ReadLine()?.Trim() ?? "";
            rightPath = UsbPathResolver.CreateUsbUri(selectedDrive, sub);
            usbSerial = selectedDrive.VolumeSerialNumber;
            usbLabel = selectedDrive.VolumeName;

            Console.Write("Czy synchronizować automatycznie po wpięciu tego pendrive'a? (t/n): ");
            autoSync = Console.ReadLine()?.Trim().ToLower().StartsWith("t") == true;
        }
        else
        {
            Console.WriteLine("Nieprawidłowy wybór, anulowano.");
            return;
        }
    }
    else
    {
        Console.Write("Ścieżka docelowa (Right): ");
        rightPath = Console.ReadLine()?.Trim() ?? "";
    }

    Console.WriteLine("\nKierunek synchronizacji:");
    Console.WriteLine(" 1. Kopia 1-kierunkowa (Backup: Left ➡️ Right)");
    Console.WriteLine(" 2. Synchronizacja 2-kierunkowa (TwoWay: Left 🔄 Right)");
    Console.Write("Wybór > ");
    var dirChoice = Console.ReadLine()?.Trim();
    var direction = dirChoice == "2" ? SyncDirection.TwoWaySync : SyncDirection.OneWayBackup;

    var job = new SyncJob
    {
        Name = name,
        LeftPath = left,
        RightPath = rightPath,
        Direction = direction,
        AutoSyncOnUsbConnect = autoSync,
        TargetUsbVolumeSerial = usbSerial,
        TargetUsbVolumeLabel = usbLabel
    };

    await jobManager.AddJobAsync(job);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n✔️ Zadanie '{job.Name}' zostało pomyślnie zapisane!");
    Console.ResetColor();
}

static async Task RunJobInteractiveAsync(IJobManager jobManager, ISyncEngine syncEngine)
{
    var jobs = jobManager.GetAllJobs();
    if (jobs.Count == 0)
    {
        Console.WriteLine("Brak zdefiniowanych zadań.");
        return;
    }

    ShowJobs(jobManager);
    Console.Write("\nWybierz numer zadania do uruchomienia: ");
    if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 1 || idx > jobs.Count)
    {
        Console.WriteLine("Nieprawidłowy numer zadania.");
        return;
    }

    var job = jobs[idx - 1];

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n[KROK 1: ANALIZA] Trwa analiza zadania '{job.Name}'...");
    Console.ResetColor();

    var progress = new Progress<SyncProgressReport>(report =>
    {
        if (!string.IsNullOrEmpty(report.CurrentItemPath))
        {
            Console.WriteLine($"  {report.Phase} {report.CurrentItemPath}");
        }
    });

    SyncPlan plan;
    try
    {
        plan = await syncEngine.AnalyzeAsync(job, progress);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Błąd podczas analizy: {ex.Message}");
        Console.ResetColor();
        return;
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n--- PLAN SYNCHRONIZACJI ---");
    Console.WriteLine($"Liczba wszystkich plików: {plan.TotalItemsCount}");
    Console.WriteLine($"Wymagane akcje:          {plan.ActionRequiredCount}");
    Console.WriteLine($"  ➡️ Kopiuj Left->Right:  {plan.LeftToRightCount}");
    Console.WriteLine($"  ⬅️ Kopiuj Right->Left:  {plan.RightToLeftCount}");
    Console.WriteLine($"  ❌ Usuń:                {plan.DeleteCount}");
    Console.WriteLine($"  ✔️ Już zsynchronizowane: {plan.InSyncCount}");
    Console.WriteLine($"  ⚠️ Konflikty:           {plan.ConflictCount}");
    Console.WriteLine($"Rozmiar do przesłania:   {FormatBytes(plan.TotalBytesToTransfer)}");
    Console.ResetColor();

    if (plan.ActionRequiredCount == 0)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✨ Wszystkie pliki są w 100% zsynchronizowane! Brak akcji do wykonania.");
        Console.ResetColor();
        return;
    }

    Console.WriteLine("\nSzczegóły zaplanowanych akcji:");
    foreach (var item in plan.Items.Where(i => i.RequiresAction || i.IsConflict))
    {
        Console.WriteLine($"  {item.ActionIcon} {item.RelativePath} ({item.StatusDescription})");
    }

    Console.Write("\nCzy chcesz wykonać powyższą synchronizację? (t/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLower();
    if (confirm != "t" && confirm != "tak" && confirm != "y")
    {
        Console.WriteLine("Anulowano synchronizację.");
        return;
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n[KROK 2: WYKONANIE] Synchronizowanie...");
    Console.ResetColor();

    var execProgress = new Progress<SyncProgressReport>(r =>
    {
        if (r.TotalBytes > 0)
        {
            var speedMb = r.BytesPerSecond / (1024 * 1024);
            Console.Write($"\rPostęp: {r.ProcessedItems}/{r.TotalItems} ({r.BytesPercentage:0.0}%) - {speedMb:0.0} MB/s [{r.CurrentItemPath}]      ");
        }
    });

    var result = await syncEngine.ExecuteAsync(job, plan, execProgress);
    Console.WriteLine();

    await jobManager.UpdateJobAsync(job);

    if (result.Success)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n🎉 Synchronizacja zakończona pomyślnie w {result.Duration.TotalSeconds:0.00}s!");
        Console.WriteLine($"   Skopiowano: {result.FilesCopied} plików ({FormatBytes(result.TotalBytesTransferred)})");
        Console.WriteLine($"   Usunięto:   {result.FilesDeleted} plików");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n❌ Zakończono z {result.ErrorsCount} błędami:");
        foreach (var err in result.Errors)
        {
            Console.WriteLine($"   - {err}");
        }
        Console.ResetColor();
    }
}

static async Task RunDemoTestAsync(ISyncEngine engine, IUsbDeviceWatcher watcher)
{
    var demoBase = Path.Combine(Path.GetTempPath(), "FileSyncDemo_" + Guid.NewGuid().ToString("N"));
    var demoLeft = Path.Combine(demoBase, "Source_PC");
    var demoRight = Path.Combine(demoBase, "Target_Backup");

    Directory.CreateDirectory(demoLeft);
    Directory.CreateDirectory(demoRight);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"--- Szybki Test Demonstracyjny ---");
    Console.WriteLine($"Folder źródłowy: {demoLeft}");
    Console.WriteLine($"Folder docelowy:  {demoRight}\n");
    Console.ResetColor();

    // Create demo files
    await File.WriteAllTextAsync(Path.Combine(demoLeft, "Raport_2026.docx"), "Zawartość raportu...");
    await File.WriteAllTextAsync(Path.Combine(demoLeft, "Dane_Finansowe.xlsx"), "Tabela finansowa...");
    Directory.CreateDirectory(Path.Combine(demoLeft, "Zdjecia"));
    await File.WriteAllTextAsync(Path.Combine(demoLeft, "Zdjecia", "Wakacje.jpg"), "Dane zdjęcia...");

    var job = new SyncJob
    {
        Name = "Demo Backup",
        LeftPath = demoLeft,
        RightPath = demoRight,
        Direction = SyncDirection.OneWayBackup
    };

    Console.WriteLine("1. Analiza różnic...");
    var plan = await engine.AnalyzeAsync(job);
    Console.WriteLine($"   Wykryto {plan.Items.Count} plików do skopiowania.");

    Console.WriteLine("2. Wykonanie synchronizacji...");
    var result = await engine.ExecuteAsync(job, plan);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"   ✔️ Skopiowano {result.FilesCopied} plików w {result.Duration.TotalMilliseconds:0}ms.");
    Console.ResetColor();

    Console.WriteLine("3. Dodanie nowego pliku w źródle i powtórna analiza...");
    await File.WriteAllTextAsync(Path.Combine(demoLeft, "Nowa_Notatka.txt"), "Nowa notatka!");
    var plan2 = await engine.AnalyzeAsync(job);
    Console.WriteLine($"   Wykryto {plan2.ActionRequiredCount} plik(i) wymagający akcji ({plan2.Items.First(i => i.RequiresAction).RelativePath}).");

    var result2 = await engine.ExecuteAsync(job, plan2);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"   ✔️ Zsynchronizowano przyrostowo ({result2.FilesCopied} plik dodany).");
    Console.ResetColor();

    // Clean up
    try { Directory.Delete(demoBase, true); } catch { }
}

static void ListenUsbEvents(IUsbDeviceWatcher watcher)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("--- Nasłuchiwanie zdarzeń USB w czasie rzeczywistym ---");
    Console.WriteLine("Wepnij lub odepnij pendrive... (Naciśnij dowolny klawisz, aby powrócić do menu)");
    Console.ResetColor();

    void OnConnected(object? s, DriveDeviceInfo d)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[🟢 WPIĘTO URZĄDZENIE]: {d.DisplayName}");
        Console.WriteLine($"   Litera dysku: {d.RootPath} | Serial: {d.VolumeSerialNumber} | Format: {d.DriveFormat}");
        Console.WriteLine($"   URI: {UsbPathResolver.CreateUsbUri(d)}");
        Console.ResetColor();
    }

    void OnDisconnected(object? s, string root)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[🔴 ODPIĘTO URZĄDZENIE]: {root}");
        Console.ResetColor();
    }

    watcher.DriveConnected += OnConnected;
    watcher.DriveDisconnected += OnDisconnected;

    Console.ReadKey(intercept: true);

    watcher.DriveConnected -= OnConnected;
    watcher.DriveDisconnected -= OnDisconnected;
    Console.WriteLine("Zakończono nasłuchiwanie.");
}

static string FormatBytes(long bytes)
{
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

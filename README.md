# 🔄 FileSynchronizer (Klon GoodSync w technologii C# .NET 9)

Zaawansowany program do synchronizacji i tworzenia kopii zapasowych plików między folderami komputera a nośnikami USB, wzorowany na mechanizmach programu **GoodSync**.

---

## ✨ Główne Możliwości

1. **Dwufazowy proces synchronizacji (w stylu GoodSync):**
   * **Krok 1: 🔍 Analiza (Diff / Dry-run):** Szybkie skanowanie, porównanie z bazą stanu (snapshot) oraz wyliczenie planu akcji z wizualizacją (`➡️ Kopiuj`, `⬅️ Kopiuj`, `❌ Usuń`, `⚠️ Konflikt`, `✔️ Identyczny`).
   * **Krok 2: 🔄 Synchronizacja (Execution):** Bezpieczne kopiowanie strumieniowe (zapis do pliku tymczasowego i atomowa podmiana), zachowanie oryginalnych metadanych i znaczników czasu (`CreationTime`, `LastWriteTime`).
2. **Inteligentne rozpoznawanie USB:**
   * Urządzenia USB są identyfikowane po unikalnym **Volume Serial Number** lub **Etykiecie nośnika**.
   * Jeśli Windows zmieni literę dysku (np. z `E:\` na `F:\`), zadanie nadal działa bez konieczności ponownej konfiguracji.
3. **⚡ Auto-Sync po wpięciu Pendrive'a:**
   * Usługa w tle nasłuchuje zdarzeń systemowych Windows (Win32 API + WMI).
   * Po wpięciu zdefiniowanego pendrive'a program może automatycznie uruchomić synchronizację i powiadomić o zakończeniu.
4. **Tryby synchronizacji:**
   * **Kopia 1-kierunkowa (Backup):** Kopiowanie ze źródła do celu z opcją usuwania plików osieroconych (kopia lustrzana).
   * **Synchronizacja 2-kierunkowa (TwoWay):** Propagacja zmian z obu stron z wykrywaniem konfliktów.
5. **Wysoka wydajność:**
   * Szybkie sumy kontrolne **xxHash64** (`System.IO.Hashing`).
   * Wykluczenia filtrów glob (`*.tmp`, `Thumbs.db`, `.git/*`, itp.).
6. **Nowoczesny interfejs graficzny (WPF Dark UI):**
   * Podział ekranu (Split View) z tabelą porównawczą plików.
   * Pasek boczny z listą zadań i podłączonych urządzeń USB.
   * Wskaźniki postępu i prędkości transferu w czasie rzeczywistym (`MB/s`).

---

## 📂 Struktura Projektu

```
FileSynchronizer/
├── FileSynchronizer.sln
├── src/
│   ├── FileSynchronizer.Core/          # Rdzeń logiczny (Silnik, USB Watcher, Hashing, Snapshoty)
│   ├── FileSynchronizer.UI/            # Aplikacja Desktopowa WPF (Modern Dark UI, MVVM)
│   └── FileSynchronizer.Cli/           # Narzędzie konsolowe / runner testowy
└── tests/
    └── FileSynchronizer.Core.Tests/    # Pakiet testów xUnit (16 testów)
```

---

## 🚀 Uruchomienie

### Uruchomienie aplikacji okienkowej (WPF):
```powershell
cd FileSynchronizer
dotnet run --project src/FileSynchronizer.UI
```

### Uruchomienie wersji konsolowej (CLI):
```powershell
dotnet run --project src/FileSynchronizer.Cli
```

### Uruchomienie testów jednostkowych:
```powershell
dotnet test
```

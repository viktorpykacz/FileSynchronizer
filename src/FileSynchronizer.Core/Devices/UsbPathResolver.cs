using System.IO;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.Core.Devices;

/// <summary>
/// Resolves paths, supporting both regular filesystem paths and dynamic USB URI formats.
/// Format examples:
/// - "usb://SERIAL_A1B2C3D4/Backups" -> Resolves to "E:\Backups" where E: has serial "A1B2C3D4"
/// - "usb://LABEL:PENDRIVE/Backups" -> Resolves to "E:\Backups" where E: has label "PENDRIVE"
/// - "C:\Folder\Subfolder" -> Normal direct path
/// </summary>
public static class UsbPathResolver
{
    public const string UsbSchemePrefix = "usb://";

    public static bool IsUsbDynamicPath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && path.StartsWith(UsbSchemePrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a dynamic USB URI from a given drive info and relative subfolder.
    /// </summary>
    public static string CreateUsbUri(DriveDeviceInfo drive, string subfolder = "")
    {
        var cleanSubfolder = subfolder.TrimStart('\\', '/');
        // Prefer SerialNumber for uniqueness, fallback to VolumeName
        var identifier = !string.IsNullOrWhiteSpace(drive.VolumeSerialNumber)
            ? $"SERIAL:{drive.VolumeSerialNumber}"
            : $"LABEL:{drive.VolumeName}";

        return string.IsNullOrEmpty(cleanSubfolder)
            ? $"{UsbSchemePrefix}{identifier}"
            : $"{UsbSchemePrefix}{identifier}/{cleanSubfolder}";
    }

    /// <summary>
    /// Resolves a path (static or USB dynamic) against currently available drives.
    /// </summary>
    public static (bool Success, string ResolvedPath, string? ErrorMessage) ResolvePath(string path, IReadOnlyList<DriveDeviceInfo> availableDrives)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, string.Empty, "Ścieżka nie może być pusta.");
        }

        if (!IsUsbDynamicPath(path))
        {
            // Standard path
            return (true, Path.GetFullPath(path), null);
        }

        // Parse usb://IDENTIFIER/subpath
        var rawContent = path.Substring(UsbSchemePrefix.Length);
        var parts = rawContent.Split(new[] { '/', '\\' }, 2, StringSplitOptions.None);
        var identifier = parts[0];
        var subpath = parts.Length > 1 ? parts[1] : string.Empty;

        DriveDeviceInfo? matchedDrive = null;

        if (identifier.StartsWith("SERIAL:", StringComparison.OrdinalIgnoreCase))
        {
            var targetSerial = identifier.Substring("SERIAL:".Length).Trim();
            matchedDrive = availableDrives.FirstOrDefault(d => 
                string.Equals(d.VolumeSerialNumber?.Replace("-", ""), targetSerial.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
        }
        else if (identifier.StartsWith("LABEL:", StringComparison.OrdinalIgnoreCase))
        {
            var targetLabel = identifier.Substring("LABEL:".Length).Trim();
            matchedDrive = availableDrives.FirstOrDefault(d => 
                string.Equals(d.VolumeName, targetLabel, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // Try matching either serial or label
            matchedDrive = availableDrives.FirstOrDefault(d =>
                string.Equals(d.VolumeSerialNumber?.Replace("-", ""), identifier.Replace("-", ""), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.VolumeName, identifier, StringComparison.OrdinalIgnoreCase));
        }

        if (matchedDrive == null)
        {
            return (false, string.Empty, $"Nie znaleziono podłączonego nośnika USB o identyfikatorze: {identifier}");
        }

        var resolved = Path.Combine(matchedDrive.RootPath, subpath);
        return (true, resolved, null);
    }

    /// <summary>
    /// Converts a local filesystem path (e.g. D:\Folder) into a dynamic USB URI if the root drive is a removable USB device.
    /// </summary>
    public static (bool IsUsb, string UsbUri, DriveDeviceInfo? MatchedDrive) TryConvertPathToUsbUri(string localPath, IReadOnlyList<DriveDeviceInfo> availableDrives)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return (false, localPath, null);
        }

        if (IsUsbDynamicPath(localPath))
        {
            return (true, localPath, null);
        }

        try
        {
            var fullPath = Path.GetFullPath(localPath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                return (false, localPath, null);
            }

            var matchedDrive = availableDrives.FirstOrDefault(d =>
                string.Equals(d.RootPath.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));

            if (matchedDrive != null && matchedDrive.DriveType == DriveType.Removable)
            {
                var relativeSub = Path.GetRelativePath(matchedDrive.RootPath, fullPath).Replace('\\', '/');
                if (relativeSub == "." || relativeSub == "/") relativeSub = "";

                var uri = CreateUsbUri(matchedDrive, relativeSub);
                return (true, uri, matchedDrive);
            }
        }
        catch
        {
        }

        return (false, localPath, null);
    }

    /// <summary>
    /// Returns a human-friendly label for a path (e.g. "[Pendrive KINGSTON (D:\)] /Dokumenty").
    /// </summary>
    public static string GetFriendlyDisplayPath(string path, IReadOnlyList<DriveDeviceInfo> availableDrives)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Nie wybrano folderu";

        if (!IsUsbDynamicPath(path))
        {
            return path;
        }

        var (success, resolved, _) = ResolvePath(path, availableDrives);
        var rawContent = path.Substring(UsbSchemePrefix.Length);
        var parts = rawContent.Split(new[] { '/', '\\' }, 2, StringSplitOptions.None);
        var subpath = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? $"/{parts[1]}" : "/ (katalog główny)";

        if (success && !string.IsNullOrEmpty(resolved))
        {
            var driveRoot = Path.GetPathRoot(resolved);
            var matchedDrive = availableDrives.FirstOrDefault(d =>
                string.Equals(d.RootPath.TrimEnd('\\', '/'), driveRoot?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));

            var driveName = matchedDrive != null && !string.IsNullOrWhiteSpace(matchedDrive.VolumeName)
                ? matchedDrive.VolumeName
                : "Pendrive USB";

            return $"🔌 [Pendrive: {driveName} ({driveRoot})] {subpath}";
        }

        return $"🔌 [Pendrive: {parts[0]}] {subpath} (odłączony)";
    }
}

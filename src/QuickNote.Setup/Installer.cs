using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace QuickNote.Setup;

public sealed record InstallOptions(string InstallPath, bool CreateDesktopShortcut, bool AssociateSecureNotes);

/// <summary>Per-user Windows installation (no admin rights required).</summary>
public static class Installer
{
    private const string AppName = "QuickNote";
    private const string ExecutableName = "QuickNote.exe";
    private const string UninstallerName = "Uninstall.exe";
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickNote";
    private const string FileTypeId = "QuickNote.SecureNote";

    public static string Version { get; } = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "local";

    public static string DefaultInstallPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    public static bool HasPayload => Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("payload.zip");

    public static string ReadInstalledPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
        return key?.GetValue("InstallLocation") as string ?? DefaultInstallPath;
    }

    public static async Task InstallAsync(InstallOptions options, IProgress<(double Percent, string Message)> progress)
    {
        await Task.Run(() =>
        {
            progress.Report((0.05, "Vorbereiten …"));
            StopRunningInstances(options.InstallPath);

            progress.Report((0.15, "Dateien kopieren …"));
            ExtractPayload(options.InstallPath, progress);

            progress.Report((0.80, "Deinstallation einrichten …"));
            var uninstallerPath = Path.Combine(options.InstallPath, UninstallerName);
            File.Copy(Environment.ProcessPath!, uninstallerPath, overwrite: true);
            RegisterUninstaller(options.InstallPath, uninstallerPath);

            progress.Report((0.88, "Verknüpfungen erstellen …"));
            var executablePath = Path.Combine(options.InstallPath, ExecutableName);
            CreateShortcut(StartMenuShortcutPath, executablePath);
            if (options.CreateDesktopShortcut)
            {
                CreateShortcut(DesktopShortcutPath, executablePath);
            }

            if (options.AssociateSecureNotes)
            {
                progress.Report((0.95, "Dateityp .txts registrieren …"));
                RegisterSecureNoteFileType(executablePath);
            }

            progress.Report((1.0, "Fertig"));
        });
    }

    public static async Task UninstallAsync(string installPath)
    {
        await Task.Run(() =>
        {
            StopRunningInstances(installPath);
            File.Delete(StartMenuShortcutPath);
            File.Delete(DesktopShortcutPath);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{FileTypeId}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.txts", throwOnMissingSubKey: false);
            ScheduleFolderRemoval(installPath);
        });
    }

    public static void StartApplication(string installPath)
        => Process.Start(new ProcessStartInfo(Path.Combine(installPath, ExecutableName)) { UseShellExecute = true });

    private static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

    private static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");

    private static void ExtractPayload(string installPath, IProgress<(double Percent, string Message)> progress)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException("Dieses Setup enthält keine Programmdateien.");
        using var archive = new ZipArchive(payload);

        if (Directory.Exists(installPath))
        {
            Directory.Delete(installPath, recursive: true);
        }

        Directory.CreateDirectory(installPath);
        var fullInstallPath = Path.GetFullPath(installPath) + Path.DirectorySeparatorChar;
        var fileCount = archive.Entries.Count;
        for (var index = 0; index < fileCount; index++)
        {
            var entry = archive.Entries[index];
            var targetPath = Path.GetFullPath(Path.Combine(installPath, entry.FullName));
            if (!targetPath.StartsWith(fullInstallPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Ungültiger Pfad im Setup-Archiv.");
            }

            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
            progress.Report((0.15 + 0.6 * (index + 1) / fileCount, $"Kopiere {entry.Name} …"));
        }
    }

    private static void RegisterUninstaller(string installPath, string uninstallerPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", "Matthias");
        key.SetValue("InstallLocation", installPath);
        key.SetValue("DisplayIcon", Path.Combine(installPath, ExecutableName));
        key.SetValue("UninstallString", $"\"{uninstallerPath}\" /uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void RegisterSecureNoteFileType(string executablePath)
    {
        using (var extensionKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.txts"))
        {
            extensionKey.SetValue(string.Empty, FileTypeId);
        }

        using var typeKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{FileTypeId}");
        typeKey.SetValue(string.Empty, "QuickNote verschlüsselte Notiz");
        using (var iconKey = typeKey.CreateSubKey("DefaultIcon"))
        {
            iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");
        }

        using var commandKey = typeKey.CreateSubKey(@"shell\open\command");
        commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        var script = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut('{Escape(shortcutPath)}');" +
                     $"$s.TargetPath='{Escape(targetPath)}';$s.WorkingDirectory='{Escape(Path.GetDirectoryName(targetPath)!)}';$s.Save()";
        RunHidden("powershell", $"-NoProfile -NonInteractive -Command \"{script}\"");
    }

    private static string Escape(string value) => value.Replace("'", "''");

    private static void StopRunningInstances(string installPath)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExecutableName)))
        {
            try
            {
                if (process.MainModule?.FileName.StartsWith(installPath, StringComparison.OrdinalIgnoreCase) == true)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Process already exited or is inaccessible; nothing to stop.
            }
        }
    }

    /// <summary>The running uninstaller lives inside the folder, so the folder is removed after this process exits.</summary>
    private static void ScheduleFolderRemoval(string installPath)
        => RunHidden("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{installPath}\"", wait: false);

    private static void RunHidden(string fileName, string arguments, bool wait = true)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments) { CreateNoWindow = true, UseShellExecute = false };
        using var process = Process.Start(startInfo);
        if (wait)
        {
            process?.WaitForExit();
        }
    }
}

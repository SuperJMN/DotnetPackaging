using System;
using System.Diagnostics;
using System.IO;

namespace DotnetPackaging.Exe.UninstallLauncher;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            // 1. Get launcher's own directory (installation directory)
            var launcherPath = Environment.ProcessPath;
            if (launcherPath is null)
            {
                Console.Error.WriteLine("ERROR: Cannot determine launcher path");
                return 1;
            }

            var installDir = Path.GetDirectoryName(launcherPath);
            if (installDir is null)
            {
                Console.Error.WriteLine("ERROR: Cannot determine installation directory");
                return 1;
            }

            var uninstallerSource = Path.Combine(installDir, "Uninstall.exe");
            if (!File.Exists(uninstallerSource))
            {
                Console.Error.WriteLine($"ERROR: Uninstaller not found at {uninstallerSource}");
                return 1;
            }

            // 2. Copy uninstaller to %TEMP% with unique directory
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "DotnetPackaging",
                "Uninstallers",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempDir);

            var uninstallerTemp = Path.Combine(tempDir, "Uninstall.exe");
            File.Copy(uninstallerSource, uninstallerTemp, overwrite: true);

            // 3. Launch uninstaller from %TEMP%
            var psi = new ProcessStartInfo
            {
                FileName = uninstallerTemp,
                Arguments = "--uninstall",
                UseShellExecute = false,
                WorkingDirectory = tempDir
            };

            var process = Process.Start(psi);
            if (process is null)
            {
                Console.Error.WriteLine("ERROR: Failed to start uninstaller");
                return 1;
            }

            // 4. Wait for uninstaller to complete
            // This prevents Windows from showing "program may not have uninstalled correctly" dialog
            process.WaitForExit();
            
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }
}

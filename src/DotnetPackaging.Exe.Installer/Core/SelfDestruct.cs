using System.Diagnostics;
using System.IO;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class SelfDestruct
{
    public static void Schedule(string targetFile, string targetDir)
    {
        // Wait 3 seconds for the application to exit completely, then delete the file and the directory.
        var cmd = $"/C timeout /t 3 /nobreak > NUL & del \"{targetFile}\" & rmdir /s /q \"{targetDir}\"";
        
        var info = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = cmd,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        
        Process.Start(info);
    }
}

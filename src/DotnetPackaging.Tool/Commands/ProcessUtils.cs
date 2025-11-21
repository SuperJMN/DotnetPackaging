using System.Diagnostics;
using CSharpFunctionalExtensions;
using System.IO;

namespace DotnetPackaging.Tool.Commands;

public static class ProcessUtils
{
    public static void MakeExecutable(string path)
    {
        try
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                       UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            if (File.Exists(path))
            {
                File.SetUnixFileMode(path, mode);
            }
        }
        catch
        {
            // ignore; best-effort
        }
    }

    public static Result Run(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                var err = proc.StandardError.ReadToEnd();
                var stdout = proc.StandardOutput.ReadToEnd();
                return Result.Failure($"{fileName} {arguments}\nExitCode: {proc.ExitCode}\n{stdout}\n{err}");
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}

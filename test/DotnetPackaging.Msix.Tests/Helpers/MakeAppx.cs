using System.Diagnostics;
using System.IO.Compression;

namespace MsixPackaging.Tests.Helpers;

public static class MakeAppx
{
    public static async Task<MsixUnpackResult> UnpackMsixAsync(
        string msixPath,
        string outputDirectory,
        string? workingDirectory = null,
        int timeoutSeconds = 300)
    {
        if (!File.Exists(msixPath))
        {
            return new MsixUnpackResult
            {
                Success = false,
                ErrorMessage = $"El archivo MSIX no existe: {msixPath}"
            };
        }

        try
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            return new MsixUnpackResult
            {
                Success = false,
                ErrorMessage = $"No se pudo limpiar el directorio de salida '{outputDirectory}': {ex.Message}",
                Exception = ex
            };
        }

        Directory.CreateDirectory(outputDirectory);

        var workingDir = workingDirectory ?? Directory.GetCurrentDirectory();

        if (OperatingSystem.IsWindows())
        {
            var makeAppxResult = await TryRunMakeAppxAsync(msixPath, outputDirectory, workingDir, timeoutSeconds);
            if (makeAppxResult != null)
            {
                return makeAppxResult;
            }
        }

        return ExtractWithZipArchive(msixPath, outputDirectory);
    }

    private static async Task<MsixUnpackResult?> TryRunMakeAppxAsync(
        string msixPath,
        string outputDirectory,
        string workingDirectory,
        int timeoutSeconds)
    {
        if (!IsMakeAppxAvailable())
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "makeappx",
                Arguments = $"unpack /p \"{msixPath}\" /d \"{outputDirectory}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            }
        };

        try
        {
            if (!process.Start())
            {
                return new MsixUnpackResult
                {
                    Success = false,
                    ErrorMessage = "No se pudo iniciar el proceso makeappx."
                };
            }

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            Task finishedTask;
            if (timeoutSeconds > 0)
            {
                var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                finishedTask = await Task.WhenAny(process.WaitForExitAsync(), delayTask);
                if (finishedTask == delayTask)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // Ignorar errores al matar el proceso
                    }

                    return new MsixUnpackResult
                    {
                        Success = false,
                        ExitCode = -1,
                        ErrorMessage = "El proceso makeappx se bloqueó y se agotó el tiempo de espera."
                    };
                }
            }
            else
            {
                await process.WaitForExitAsync();
            }

            var standardOutput = await stdOutTask.ConfigureAwait(false);
            var errorOutput = await stdErrTask.ConfigureAwait(false);

            return new MsixUnpackResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                ErrorOutput = errorOutput,
                ErrorMessage = process.ExitCode != 0
                    ? $"Error al desempaquetar MSIX con makeappx. Código de salida: {process.ExitCode}"
                    : null
            };
        }
        catch (Exception ex)
        {
            return new MsixUnpackResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Excepción al ejecutar makeappx: {ex.Message}",
                Exception = ex
            };
        }
    }

    private static MsixUnpackResult ExtractWithZipArchive(string msixPath, string outputDirectory)
    {
        try
        {
            using var archive = ZipFile.OpenRead(msixPath);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(outputDirectory, entry.FullName));
                if (!destinationPath.StartsWith(Path.GetFullPath(outputDirectory), StringComparison.Ordinal))
                {
                    return new MsixUnpackResult
                    {
                        Success = false,
                        ExitCode = -1,
                        ErrorMessage = $"Entrada fuera del directorio de destino: {entry.FullName}"
                    };
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }

            return new MsixUnpackResult
            {
                Success = true,
                ExitCode = 0,
                StandardOutput = "Desempaquetado mediante System.IO.Compression",
                ErrorOutput = string.Empty
            };
        }
        catch (Exception ex)
        {
            return new MsixUnpackResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Error al desempaquetar MSIX: {ex.Message}",
                Exception = ex
            };
        }
    }

    private static bool IsMakeAppxAvailable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return false;
        }

        foreach (var directory in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var trimmed = directory.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var candidate = Path.Combine(trimmed, "makeappx.exe");
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
            catch
            {
                // Ignorar rutas no válidas
            }
        }

        return false;
    }
}

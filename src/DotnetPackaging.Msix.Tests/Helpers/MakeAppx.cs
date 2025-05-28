using System.Diagnostics;

namespace MsixPackaging.Tests.Helpers;

public static class MakeAppx
{
    /// <summary>
    /// Desempaqueta un archivo MSIX de manera robusta, evitando bloqueos.
    /// </summary>
    /// <param name="msixPath">Ruta completa al archivo MSIX</param>
    /// <param name="outputDirectory">Directorio de destino para los archivos desempaquetados</param>
    /// <param name="workingDirectory">Working directory during execution (null to use the current directory)</param>
    /// <param name="timeoutSeconds">Tiempo máximo en segundos para esperar que termine el proceso (0 para esperar indefinidamente)</param>
    /// <returns>Un objeto con el resultado de la operación</returns>
    public static async Task<MsixUnpackResult> UnpackMsixAsync(
        string msixPath,
        string outputDirectory,
        string workingDirectory = null,
        int timeoutSeconds = 300) // 5 minutos por defecto
    {
        try
        {
            Directory.Delete(outputDirectory, true);
        }
        catch
        {
        }

        // Verificar que el archivo MSIX existe
        if (!File.Exists(msixPath))
            return new MsixUnpackResult
            {
                Success = false,
                ErrorMessage = $"El archivo MSIX no existe: {msixPath}"
            };

        // Asegurarse de que el directorio de salida exista
        Directory.CreateDirectory(outputDirectory);

        // Determinar el directorio de trabajo
        string actualWorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();

        // Crear un token de cancelación para el timeout si es necesario
        using var cts = timeoutSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
            : new CancellationTokenSource();

        try
        {
            // Configurar el proceso
            var startInfo = new ProcessStartInfo
            {
                FileName = "makeappx",
                Arguments = $"unpack /p \"{msixPath}\" /d \"{outputDirectory}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = actualWorkingDirectory
            };

            // Esta es una estrategia más segura, usando Process.Start de forma sincrónica
            using var process = Process.Start(startInfo);

            if (process == null)
                return new MsixUnpackResult
                {
                    Success = false,
                    ErrorMessage = "No se pudo iniciar el proceso makeappx."
                };

            // Leer la salida estándar y de error en tareas separadas para evitar bloqueos
            var readOutputTask = ReadStreamToEndAsync(process.StandardOutput, cts.Token);
            var readErrorTask = ReadStreamToEndAsync(process.StandardError, cts.Token);

            // Esperar a que termine el proceso
            bool exited = process.WaitForExit(timeoutSeconds > 0 ? timeoutSeconds * 1000 : -1);

            if (!exited)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // Ignorar errores al intentar matar el proceso
                }

                return new MsixUnpackResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "El proceso se bloqueó y se agotó el tiempo de espera."
                };
            }

            // Intentar obtener las salidas
            string standardOutput = await readOutputTask;
            string errorOutput = await readErrorTask;

            return new MsixUnpackResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                ErrorOutput = errorOutput,
                ErrorMessage = process.ExitCode != 0
                    ? $"Error al desempaquetar MSIX. Código de salida: {process.ExitCode}"
                    : null
            };
        }
        catch (OperationCanceledException)
        {
            return new MsixUnpackResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "La operación fue cancelada por timeout."
            };
        }
        catch (Exception ex)
        {
            return new MsixUnpackResult
            {
                Success = false,
                ErrorMessage = $"Excepción al ejecutar makeappx: {ex.Message}",
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Lee un StreamReader hasta el final de forma asíncrona con soporte de cancelación
    /// </summary>
    private static async Task<string> ReadStreamToEndAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadToEndAsync();
        }
        catch (OperationCanceledException)
        {
            return "[Lectura cancelada por timeout]";
        }
        catch (Exception ex)
        {
            return $"[Error al leer la salida: {ex.Message}]";
        }
    }
}
namespace DotnetPackaging.Deployment.Platforms.Android;

public class AndroidSdk(Maybe<ILogger> logger)
{
    public Result<string> FindPath()
    {
        logger.Execute(log => log.Information("Intentando autodetectar Android SDK..."));

        // Rutas comunes donde se suele instalar Android SDK
        var commonPaths = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Android", "Sdk"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk"),
            "/usr/lib/android-sdk",
            "/opt/android-sdk",
            "/home/android-sdk"
        };

        // También verificar variables de entorno
        var envPath = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ??
                      Environment.GetEnvironmentVariable("ANDROID_HOME");

        var pathsToCheck = envPath != null
            ? new[] { envPath }.Concat(commonPaths)
            : commonPaths;

        foreach (var path in pathsToCheck)
        {
            var validationResult = Check(path);
            if (validationResult.IsSuccess)
            {
                logger.Execute(log => log.Information("Android SDK autodetectado en: {Path}", path));
                return validationResult;
            }
        }

        var errorMessage = "No se pudo autodetectar Android SDK. Rutas verificadas: " + string.Join(", ", pathsToCheck);
        logger.Execute(log => log.Error(errorMessage));
        return Result.Failure<string>(errorMessage);
    }

    public Result<string> Check(string androidSdkPath)
    {
        if (string.IsNullOrWhiteSpace(androidSdkPath))
            return Result.Failure<string>("La ruta del Android SDK está vacía");

        if (!System.IO.Directory.Exists(androidSdkPath))
            return Result.Failure<string>($"La ruta del Android SDK no existe: {androidSdkPath}");

        // Verificar que contenga directorios típicos de Android SDK
        var requiredDirs = new[] { "platform-tools", "platforms" };
        var missingDirs = requiredDirs.Where(dir => !System.IO.Directory.Exists(System.IO.Path.Combine(androidSdkPath, dir))).ToList();

        if (missingDirs.Any())
            return Result.Failure<string>($"La ruta no parece ser un Android SDK válido. Faltan directorios: {string.Join(", ", missingDirs)}");

        return Result.Success(androidSdkPath);
    }
}
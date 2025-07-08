using System.Reactive.Linq;
using File = System.IO.File;

namespace DotnetPackaging.Deployment.Platforms.Android;

public class AndroidDeployment(IDotnet dotnet, Path projectPath, AndroidDeployment.DeploymentOptions options, Maybe<ILogger> logger)
{
    public async Task<Result<IEnumerable<INamedByteSource>>> Create()
    {
        var tempKeystoreResult = await CreateTempKeystore(options.AndroidSigningKeyStore);

        return await tempKeystoreResult
            .Bind(async tempKeystore =>
            {
                var sdk = new AndroidSdk(logger);
                
                var androidSdkPathResult = options.AndroidSdkPath
                    .Match(path => sdk.Check(path), () => new AndroidSdk(logger).FindPath());
                
                using (tempKeystore)
                {
                    return await androidSdkPathResult
                        .Bind(async androidSdkPath =>
                        {
                            var args = CreateArgs(options, tempKeystore.FilePath, androidSdkPath);
                            var publishResult = await dotnet.Publish(projectPath, args);
                            return publishResult.Map(ApkFiles);
                        });
                }
            });
    }


    private static async Task<Result<TempKeystoreFile>> CreateTempKeystore(IByteSource byteSource)
    {
        return await Result.Try(async () =>
        {
            var tempPath = System.IO.Path.GetTempFileName();
            var tempFile = new TempKeystoreFile(tempPath);

            await using var stream = File.OpenWrite(tempPath);
            await byteSource.WriteTo(stream);

            return tempFile;
        });
    }

    private static IEnumerable<INamedByteSource> ApkFiles(IContainer directory)
    {
        return directory.FilesWithPathsRecursive()
            .Where(file => file.Name.EndsWith(".apk"));
    }

    private static string CreateArgs(DeploymentOptions deploymentOptions, string keyStorePath, string androidSdkPath)
    {
        var properties = new[]
        {
            new[] { "ApplicationVersion", deploymentOptions.ApplicationVersion.ToString() },
            new[] { "ApplicationDisplayVersion", deploymentOptions.ApplicationDisplayVersion },
            new[] { "AndroidKeyStore", "true" },
            new[] { "AndroidSigningKeyStore", keyStorePath },
            new[] { "AndroidSigningKeyAlias", deploymentOptions.SigningKeyAlias },
            new[] { "AndroidSigningStorePass", deploymentOptions.SigningStorePass },
            new[] { "AndroidSigningKeyPass", deploymentOptions.SigningKeyPass },
            new[] { "AndroidSdkDirectory", androidSdkPath }
        };

        return ArgumentsParser.Parse([["configuration", "Release"]], properties);
    }

    public class DeploymentOptions
    {
        public required int ApplicationVersion { get; init; }
        public required string ApplicationDisplayVersion { get; init; }
        public required IByteSource AndroidSigningKeyStore { get; init; }
        public required string SigningKeyAlias { get; init; }
        public required string SigningStorePass { get; init; }
        public required string SigningKeyPass { get; init; }
        public Maybe<Path> AndroidSdkPath { get; set; } = Maybe<Path>.None;
    }
}

internal class TempKeystoreFile(string filePath) : IDisposable
{
    public string FilePath { get; } = filePath;
    private bool disposed = false;

    public void Dispose()
    {
        if (!disposed)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // Log error si es necesario, pero no lanzar excepción en Dispose
            }

            disposed = true;
        }
    }
}

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
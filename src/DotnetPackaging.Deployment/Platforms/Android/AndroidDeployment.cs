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

    private IEnumerable<INamedByteSource> ApkFiles(IContainer directory)
    {
        return directory.ResourcesWithPathsRecursive()
            .Where(file => file.Name.EndsWith(".apk"))
            .Select(resource => new Resource(options.PackageName + "-" + options.ApplicationDisplayVersion + "-android" + ".apk", resource));
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
        public required string PackageName { get; set; }
        public required int ApplicationVersion { get; init; }
        public required string ApplicationDisplayVersion { get; init; }
        public required IByteSource AndroidSigningKeyStore { get; init; }
        public required string SigningKeyAlias { get; init; }
        public required string SigningStorePass { get; init; }
        public required string SigningKeyPass { get; init; }
        public Maybe<Path> AndroidSdkPath { get; set; } = Maybe<Path>.None;
    }
}
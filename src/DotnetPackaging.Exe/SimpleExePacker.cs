using System.IO.Compression;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;
using Zafiro.FileSystem.Core;

namespace DotnetPackaging.Exe;

public static class SimpleExePacker
{
    private const string BrandingLogoEntry = "Branding/logo.png";

    public static async Task<Result> Build(
        string stubPath,
        string publishDir,
        InstallerMetadata metadata,
        Maybe<byte[]> logoBytes,
        string outputPath)
    {
        if (!File.Exists(stubPath))
        {
            return Result.Failure($"Stub not found: {stubPath}");
        }

        if (!Directory.Exists(publishDir))
        {
            return Result.Failure($"Publish directory not found: {publishDir}");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Result.Failure("Output directory cannot be determined.");
        }

        var publishContainerResult = BuildContainerFromDirectory(publishDir);
        if (publishContainerResult.IsFailure)
        {
            return Result.Failure(publishContainerResult.Error);
        }

        var stubSource = ByteSource.FromAsyncStreamFactory(() => Task.FromResult<Stream>(File.OpenRead(stubPath)));
        var logoSource = logoBytes.Map(bytes => (IByteSource)ByteSource.FromBytes(bytes));
        var bundleResult = await Build(stubSource, publishContainerResult.Value, metadata, logoSource);
        if (bundleResult.IsFailure)
        {
            return Result.Failure(bundleResult.Error);
        }

        Directory.CreateDirectory(outputDirectory);
        var uninstallerPath = Path.Combine(outputDirectory, "Uninstaller.exe");
        await Persist(bundleResult.Value.Installer, outputPath);
        await Persist(bundleResult.Value.Uninstaller, uninstallerPath);

        return Result.Success();
    }

    public static async Task<Result<SimpleExeBundle>> Build(
        IByteSource stub,
        IContainer publishContent,
        InstallerMetadata metadata,
        Maybe<IByteSource> logoBytes)
    {
        var metadataSource = Serialize(metadata);
        var uninstallerPayloadResult = await BuildUninstallerPayload(stub, metadataSource);
        if (uninstallerPayloadResult.IsFailure)
        {
            return Result.Failure<SimpleExeBundle>(uninstallerPayloadResult.Error);
        }

        var uninstaller = new Resource("Uninstaller.exe", PayloadAppender.AppendPayload(stub, uninstallerPayloadResult.Value));

        var installerPayloadResult = await BuildInstallerPayload(metadataSource, publishContent, logoBytes, uninstaller);
        if (installerPayloadResult.IsFailure)
        {
            return Result.Failure<SimpleExeBundle>(installerPayloadResult.Error);
        }

        var installer = new Resource("Installer.exe", PayloadAppender.AppendPayload(stub, installerPayloadResult.Value));

        return Result.Success(new SimpleExeBundle(installer, uninstaller));
    }

    private static async Task Persist(INamedByteSource artifact, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var input = artifact.ToStreamSeekable();
        await using var output = File.Create(path);
        await input.CopyToAsync(output);
    }

    private static IByteSource Serialize(InstallerMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return ByteSource.FromString(json);
    }

    private static async Task<Result<IByteSource>> BuildUninstallerPayload(IByteSource stub, IByteSource metadataSource)
    {
        var entries = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
        {
            ["metadata.json"] = metadataSource,
            [$"Support/{"Uninstaller.exe"}"] = stub
        };

        var containerResult = entries.ToRootContainer();
        if (containerResult.IsFailure)
        {
            return Result.Failure<IByteSource>(containerResult.Error);
        }

        return await CreatePayloadZip(containerResult.Value);
    }

    private static async Task<Result<IByteSource>> BuildInstallerPayload(
        IByteSource metadataSource,
        IContainer publishContent,
        Maybe<IByteSource> logoBytes,
        INamedByteSource uninstaller)
    {
        var entries = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
        {
            ["metadata.json"] = metadataSource,
            [$"Support/{uninstaller.Name}"] = uninstaller
        };

        foreach (var file in publishContent.ResourcesWithPathsRecursive())
        {
            var entryPath = $"Content/{file.FullPath().ToString().Replace('\\', '/')}";
            entries[entryPath] = file;
        }

        logoBytes.Execute(bytes => entries[BrandingLogoEntry] = bytes);

        var containerResult = entries.ToRootContainer();
        if (containerResult.IsFailure)
        {
            return Result.Failure<IByteSource>(containerResult.Error);
        }

        return await CreatePayloadZip(containerResult.Value);
    }

    private static Result<RootContainer> BuildContainerFromDirectory(string root)
    {
        try
        {
            var files = Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    file => Path.GetRelativePath(root, file).Replace("\\", "/"),
                    file => (IByteSource)ByteSource.FromAsyncStreamFactory(() => Task.FromResult<Stream>(File.OpenRead(file))),
                    StringComparer.Ordinal);

            return files.ToRootContainer();
        }
        catch (Exception ex)
        {
            return Result.Failure<RootContainer>($"Failed to read directory '{root}': {ex.Message}");
        }
    }

    private static Task<Result<IByteSource>> CreatePayloadZip(IContainer container)
    {
        return Task.FromResult(Result.Success(CreateZipSource(container)));
    }

    private static IByteSource CreateZipSource(IContainer container)
    {
        return ByteSource.FromAsyncStreamFactory(async () =>
        {
            var zipStream = new MemoryStream();

            await using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var resource in container.ResourcesWithPathsRecursive())
                {
                    var entry = zip.CreateEntry(resource.FullPath().ToString().Replace('\\', '/'), CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var resourceStream = resource.ToStreamSeekable();
                    await resourceStream.CopyToAsync(entryStream);
                }
            }

            zipStream.Position = 0;
            var final = new MemoryStream();
            await zipStream.CopyToAsync(final);
            final.Position = 0;
            return final;
        });
    }
}

public sealed record SimpleExeBundle(INamedByteSource Installer, INamedByteSource Uninstaller);

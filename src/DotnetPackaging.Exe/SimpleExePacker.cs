using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

public static class SimpleExePacker
{
    private const string BrandingLogoEntry = "Branding/logo.png";

    public static Task<Result<ExeBuildArtifacts>> Build(
        IByteSource stub,
        IContainer publishDirectory,
        InstallerMetadata metadata,
        Maybe<IByteSource> logoBytes)
    {
        return Result.Try(async () =>
        {
            var metadataSource = CreateMetadataSource(metadata);

            var uninstallerPayload = await CreateUninstallerPayload(metadataSource, stub);
            var uninstaller = await AppendPayload(stub, uninstallerPayload);

            var installerPayload = await CreateInstallerPayload(publishDirectory, metadataSource, uninstaller, logoBytes);
            var installer = await AppendPayload(stub, installerPayload);

            return new ExeBuildArtifacts(installer, uninstaller);
        }, ex => ex.Message);
    }

    private static async Task<IByteSource> CreateInstallerPayload(
        IContainer publishDirectory,
        IByteSource metadata,
        IByteSource uninstaller,
        Maybe<IByteSource> logoBytes)
    {
        var files = publishDirectory
            .ResourcesWithPathsRecursive()
            .ToDictionary(
                file => $"Content/{file.FullPath().ToString().Replace('\\', '/')}",
                file => (IByteSource)file);

        files["metadata.json"] = metadata;
        files["Support/Uninstaller.exe"] = uninstaller;

        await logoBytes.Match(
            async logo =>
            {
                files[BrandingLogoEntry] = logo;
                await Task.CompletedTask;
            },
            () => Task.CompletedTask);

        var entries = files.ToDictionary(
            pair => pair.Key,
            pair => (pair.Key == "metadata.json" || pair.Key == BrandingLogoEntry)
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal);

        return await CreateZip(files, entries);
    }

    private static async Task<IByteSource> CreateUninstallerPayload(IByteSource metadata, IByteSource stub)
    {
        var files = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
        {
            ["metadata.json"] = metadata,
            ["Support/Uninstaller.exe"] = stub
        };

        var compression = new Dictionary<string, CompressionLevel>(StringComparer.Ordinal)
        {
            ["metadata.json"] = CompressionLevel.NoCompression,
            ["Support/Uninstaller.exe"] = CompressionLevel.Optimal
        };

        return await CreateZip(files, compression);
    }

    private static async Task<IByteSource> CreateZip(
        IReadOnlyDictionary<string, IByteSource> files,
        IReadOnlyDictionary<string, CompressionLevel> compressionLevels)
    {
        await using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entryCompression = compressionLevels[file.Key];
                var entry = zip.CreateEntry(file.Key, entryCompression);
                await using var entryStream = entry.Open();
                var write = await file.Value.WriteTo(entryStream);
                if (write.IsFailure)
                {
                    throw new InvalidOperationException(write.Error);
                }
            }
        }

        return ByteSource.FromBytes(stream.ToArray());
    }

    private static async Task<IByteSource> AppendPayload(IByteSource stub, IByteSource payload)
    {
        var payloadBytes = await ToBytes(payload);
        var stubBytes = await ToBytes(stub);
        var lengthBytes = BitConverter.GetBytes((long)payloadBytes.Length);
        var magicBytes = Encoding.ASCII.GetBytes("DPACKEXE1");

        await using var output = new MemoryStream();
        await output.WriteAsync(stubBytes);
        await output.WriteAsync(payloadBytes);
        await output.WriteAsync(lengthBytes);
        await output.WriteAsync(magicBytes);

        return ByteSource.FromBytes(output.ToArray());
    }

    private static async Task<byte[]> ToBytes(IByteSource source)
    {
        await using var stream = source.ToStreamSeekable();
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static IByteSource CreateMetadataSource(InstallerMetadata metadata)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return ByteSource.FromBytes(bytes);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Compression;

using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe.Artifacts;
using DotnetPackaging.Exe.Metadata;
using DotnetPackaging.Exe.Signing;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

internal static class SimpleExePacker
{
    private const string BrandingLogoEntry = "Branding/logo.png";

    public static Task<Result<ExeBuildArtifacts>> Build(
        IByteSource stub,
        IContainer publishDirectory,
        InstallerMetadata metadata,
        Maybe<IByteSource> logoBytes,
        Maybe<X509Certificate2> certificate = default)
    {
        return Result.Try(async () =>
        {
            var metadataSource = CreateMetadataSource(metadata);

            var uninstallerPayload = await CreateUninstallerPayload(metadataSource, stub);
            var uninstallerRaw = await AppendPayload(stub, uninstallerPayload);
            var uninstaller = await SignExeIfCertificate(uninstallerRaw, certificate);

            var installerPayload = await CreateInstallerPayload(publishDirectory, metadataSource, uninstaller, logoBytes, certificate);
            var installerRaw = await AppendPayload(stub, installerPayload);
            var installer = await SignExeIfCertificate(installerRaw, certificate);

            return new ExeBuildArtifacts(installer, uninstaller);
        }, ex => ex.Message);
    }

    private static async Task<IByteSource> SignExeIfCertificate(IByteSource source, Maybe<X509Certificate2> certificate)
    {
        if (certificate.HasNoValue)
            return source;

        var bytes = await ToBytes(source);
        var result = PeSigner.SignIfPe(bytes, certificate.Value);
        if (result.IsFailure)
            throw new InvalidOperationException($"Failed to sign PE: {result.Error}");

        return ByteSource.FromBytes(result.Value).WithLength(result.Value.LongLength);
    }

    private static async Task<IByteSource> CreateInstallerPayload(
        IContainer publishDirectory,
        IByteSource metadata,
        IByteSource uninstaller,
        Maybe<IByteSource> logoBytes,
        Maybe<X509Certificate2> certificate)
    {
        var files = publishDirectory
            .ResourcesWithPathsRecursive()
            .ToDictionary(
                file => $"Content/{file.FullPath().ToString().Replace('\\', '/')}",
                file => (IByteSource)file);

        // Sign all PE files in the publish directory before packaging
        if (certificate.HasValue)
        {
            var signedFiles = new Dictionary<string, IByteSource>(files.Count, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var bytes = await ToBytes(file.Value);
                var signed = PeSigner.SignIfPe(bytes, certificate.Value);
                var signedBytes = signed.IsSuccess ? signed.Value : bytes;
                signedFiles[file.Key] = ByteSource.FromBytes(signedBytes).WithLength(signedBytes.LongLength);
            }

            files = signedFiles;
        }

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
                : CompressionLevel.SmallestSize);

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
            ["Support/Uninstaller.exe"] = CompressionLevel.SmallestSize
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

        var bytes = stream.ToArray();
        return ByteSource.FromBytes(bytes).WithLength(bytes.LongLength);
    }

    private static async Task<IByteSource> AppendPayload(IByteSource stub, IByteSource payload)
    {
        var append = await PayloadAppender.Append(stub, payload).ConfigureAwait(false);
        if (append.IsFailure)
        {
            throw new InvalidOperationException(append.Error);
        }

        return append.Value;
    }

    private static async Task<byte[]> ToBytes(IByteSource source)
    {
        var bytes = await source.ReadAll().ConfigureAwait(false);
        if (bytes.IsFailure)
        {
            throw new InvalidOperationException(bytes.Error);
        }

        return bytes.Value;
    }

    private static IByteSource CreateMetadataSource(InstallerMetadata metadata)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return ByteSource.FromBytes(bytes).WithLength(bytes.LongLength);
    }
}

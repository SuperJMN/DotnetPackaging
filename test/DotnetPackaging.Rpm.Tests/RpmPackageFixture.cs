using DotnetPackaging;
using DotnetPackaging.Rpm;
using System.IO.Abstractions;
using Zafiro.DivineBytes.System.IO;

namespace DotnetPackaging.Rpm.Tests;

[CollectionDefinition("rpm-package")]
public class RpmPackageCollection : ICollectionFixture<RpmPackageFixture>
{
}

public sealed class RpmPackageFixture : IAsyncLifetime
{
    private readonly string workingDirectory = Path.Combine(Path.GetTempPath(), $"rpm-tests-{Guid.NewGuid():N}");
    private readonly string sourceDirectory;

    public RpmPackageFixture()
    {
        sourceDirectory = Path.Combine(workingDirectory, "publish");
    }

    public byte[] PackageBytes { get; private set; } = Array.Empty<byte>();
    public RpmArchive Archive { get; private set; } = new(new RpmHeader(new Dictionary<int, RpmTagValue>()), Array.Empty<byte>());
    public IReadOnlyList<CpioEntry> PayloadEntries { get; private set; } = Array.Empty<CpioEntry>();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(sourceDirectory);
        await WritePayloadAsync(sourceDirectory);

        PackageBytes = await BuildRpmAsync(sourceDirectory);
        Archive = RpmArchiveReader.Read(PackageBytes);
        var payload = DecompressPayload(Archive);
        PayloadEntries = CpioArchiveReader.Read(payload);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(workingDirectory))
        {
            try
            {
                Directory.Delete(workingDirectory, true);
            }
            catch
            {
                // Best-effort cleanup for temporary files.
            }
        }

        return Task.CompletedTask;
    }

    private static async Task<byte[]> BuildRpmAsync(string sourceDirectory)
    {
        var fs = new FileSystem();
        var dirInfo = new DirectoryInfo(sourceDirectory);
        var container = new DirectoryContainer(new DirectoryInfoWrapper(fs, dirInfo)).AsRoot();

        var metadata = new FromDirectoryOptions()
            .WithName("Sample App")
            .WithVersion("1.0.0")
            .WithComment("Sample package for tests")
            .WithSummary("Sample package for tests")
            .WithExecutableName("sample-app");

        var result = await new RpmPackager().Pack(container, metadata);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"RPM build failed: {result.Error}");
        }

        await using var stream = new MemoryStream();
        var write = await result.Value.WriteTo(stream);
        if (write.IsFailure)
        {
            throw new InvalidOperationException($"RPM build failed: {write.Error}");
        }

        return stream.ToArray();
    }

    private static async Task WritePayloadAsync(string directory)
    {
        var executablePath = Path.Combine(directory, "sample-app");
        await File.WriteAllBytesAsync(executablePath, CreateElfStub());

        var configDir = Path.Combine(directory, "config");
        Directory.CreateDirectory(configDir);
        await File.WriteAllTextAsync(Path.Combine(configDir, "settings.json"), "{ \"name\": \"demo\" }");
    }

    private static byte[] CreateElfStub()
    {
        var bytes = new byte[64];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        bytes[4] = 2; // 64-bit
        bytes[5] = 1; // Little endian
        BitConverter.GetBytes((ushort)2).CopyTo(bytes, 16); // ET_EXEC
        BitConverter.GetBytes((ushort)0x3E).CopyTo(bytes, 18); // x86_64
        return bytes;
    }

    private static byte[] DecompressPayload(RpmArchive archive)
    {
        var compressor = archive.Header.GetString(RpmTestTags.PayloadCompressor);
        if (!string.Equals(compressor, "gzip", StringComparison.OrdinalIgnoreCase))
        {
            return archive.Payload;
        }

        using var input = new MemoryStream(archive.Payload);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}

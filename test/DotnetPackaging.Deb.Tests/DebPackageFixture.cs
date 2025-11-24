using DotnetPackaging.Tool.Commands;

namespace DotnetPackaging.Deb.Tests;

[CollectionDefinition("deb-package")]
public class DebPackageCollection : ICollectionFixture<DebPackageFixture>
{
}

public sealed class DebPackageFixture : IAsyncLifetime
{
    private readonly string workingDirectory = Path.Combine(Path.GetTempPath(), $"deb-tests-{Guid.NewGuid():N}");
    private readonly string sourceDirectory;
    private readonly string outputFilePath;

    public DebPackageFixture()
    {
        sourceDirectory = Path.Combine(workingDirectory, "publish");
        outputFilePath = Path.Combine(workingDirectory, "sample-app.deb");
    }

    public IReadOnlyList<ArEntry> ArEntries { get; private set; } = Array.Empty<ArEntry>();

    public byte[] PackageBytes { get; private set; } = Array.Empty<byte>();

    public string OutputPath => outputFilePath;

    public ArEntry GetEntry(string name) => ArEntries.Single(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(sourceDirectory);
        await WritePayloadAsync(sourceDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        var exitCode = await DebCommandRunner.BuildDebAsync(sourceDirectory, outputFilePath);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Building .deb failed with exit code {exitCode}.");
        }

        PackageBytes = await File.ReadAllBytesAsync(outputFilePath);
        ArEntries = ArArchiveReader.Read(PackageBytes);
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
}

using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

/// <summary>
/// Builds an OSTree repository structure from a Flatpak build plan.
/// </summary>
internal static class OstreeRepoBuilder
{
    // Default permissions for files and directories
    private const uint DefaultUid = 0;
    private const uint DefaultGid = 0;
    private const uint DirectoryMode = 0x41ED; // drwxr-xr-x (16877)
    private const uint RegularFileMode = 0x81A4; // -rw-r--r-- (33188)
    private const uint ExecutableFileMode = 0x81ED; // -rwxr-xr-x (33261)

    public static Result<RootContainer> Build(FlatpakBuildPlan plan)
    {
        var repoFiles = new Dictionary<string, IByteSource>(StringComparer.Ordinal);

        // Build the content tree from the plan layout
        var layout = plan.ToRootContainer();
        var files = layout.ResourcesWithPathsRecursive().ToList();

        // Create file content objects and collect file entries for dirtree
        var fileEntries = new List<(string Name, byte[] Checksum)>();
        foreach (var file in files)
        {
            var relativePath = ((INamedWithPath)file).FullPath().ToString().Replace("\\", "/");
            var content = file.Array();
            var contentChecksum = OstreeEncoders.ComputeChecksum(content);
            var checksumHex = OstreeEncoders.ChecksumToHex(contentChecksum);

            // Store content object
            var objectPath = $"objects/{checksumHex[..2]}/{checksumHex[2..]}.file";
            repoFiles[objectPath] = ByteSource.FromBytes(content);

            // Collect for dirtree
            fileEntries.Add((relativePath, contentChecksum));
        }

        // Create root dirmeta object
        var rootDirmeta = OstreeEncoders.EncodeDirMeta(DefaultUid, DefaultGid, DirectoryMode);
        var rootDirmetaChecksum = OstreeEncoders.ComputeChecksum(rootDirmeta);
        var rootDirmetaHex = OstreeEncoders.ChecksumToHex(rootDirmetaChecksum);
        repoFiles[$"objects/{rootDirmetaHex[..2]}/{rootDirmetaHex[2..]}.dirmeta"] = ByteSource.FromBytes(rootDirmeta);

        // Create root dirtree object (flat structure - all files at root level conceptually)
        var rootDirtree = OstreeEncoders.EncodeDirTree(
            fileEntries,
            Enumerable.Empty<(string, byte[], byte[])>()
        );
        var rootDirtreeChecksum = OstreeEncoders.ComputeChecksum(rootDirtree);
        var rootDirtreeHex = OstreeEncoders.ChecksumToHex(rootDirtreeChecksum);
        repoFiles[$"objects/{rootDirtreeHex[..2]}/{rootDirtreeHex[2..]}.dirtree"] = ByteSource.FromBytes(rootDirtree);

        // Create commit object
        var commit = OstreeEncoders.EncodeCommit(
            treeContentsChecksum: rootDirtreeChecksum,
            dirmetaChecksum: rootDirmetaChecksum,
            subject: plan.Metadata.Name,
            body: $"Version {plan.Metadata.Version}",
            timestamp: DateTimeOffset.UtcNow,
            parentChecksum: null,
            metadata: null
        );
        var commitChecksum = OstreeEncoders.ComputeChecksum(commit);
        var commitHex = OstreeEncoders.ChecksumToHex(commitChecksum);
        repoFiles[$"objects/{commitHex[..2]}/{commitHex[2..]}.commit"] = ByteSource.FromBytes(commit);

        // Create refs
        var arch = plan.Metadata.Architecture.PackagePrefix;
        var refPath = $"refs/heads/app/{plan.AppId}/{arch}/stable";
        repoFiles[refPath] = ByteSource.FromString(commitHex);

        // Create repo config
        repoFiles["config"] = ByteSource.FromString(
            "[core]\n" +
            "repo_version=1\n" +
            "mode=bare-user\n"
        );

        // Create empty summary (optional but expected)
        repoFiles["summary"] = ByteSource.FromBytes(Array.Empty<byte>());

        return repoFiles.ToRootContainer();
    }
}

using System.Security.Cryptography;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak;

// Simplified object kinds with pluggable encoders (GVariant to be added)
internal abstract record OstreeObject(string Type)
{
    public abstract byte[] Serialize();
}

internal sealed record Blob(byte[] Content) : OstreeObject("blob")
{
    public override byte[] Serialize() => Content;
}

internal sealed record Tree(Dictionary<string, string> Entries) : OstreeObject("tree")
{
    public override byte[] Serialize()
    {
        // TODO: Replace with real GVariant tree encoding
        return OstreeEncoders.EncodeTree(Entries);
    }
}

internal sealed record Commit(string TreeChecksum, string Subject, DateTimeOffset Timestamp) : OstreeObject("commit")
{
    public override byte[] Serialize()
    {
        // TODO: Replace with real GVariant commit encoding
        return OstreeEncoders.EncodeCommit(TreeChecksum, Subject, Timestamp);
    }
}

public static class OstreeRepoBuilder
{
    public static Result<RootContainer> Build(FlatpakBuildPlan plan)
    {
        // 1) Create blob objects from layout files under commit root (metadata, files/, export/)
        var blobs = new Dictionary<string, (string checksum, IByteSource source)>(StringComparer.Ordinal);
        foreach (var res in plan.ToRootContainer().ResourcesWithPathsRecursive())
        {
            var bytes = res.Array();
            var checksum = Sha256(bytes);
            blobs[((INamedWithPath)res).FullPath().ToString()] = (checksum, ByteSource.FromBytes(bytes));
        }

        // 2) Tree maps file paths to blob checksums (flat list)
        var treeEntries = blobs.ToDictionary(k => k.Key, v => v.Value.checksum, StringComparer.Ordinal);
        var tree = new Tree(treeEntries);
        var treeBytes = tree.Serialize();
        var treeChecksum = Sha256(treeBytes);

        // 3) Commit referencing tree
        var commit = new Commit(treeChecksum, plan.Metadata.Name, DateTimeOffset.Now);
        var commitBytes = commit.Serialize();
        var commitChecksum = Sha256(commitBytes);

        // 4) Build repo layout
        var repoFiles = new Dictionary<string, IByteSource>(StringComparer.Ordinal)
        {
            ["config"] = ByteSource.FromString("[core]\nrepo_version=1\nmode=bare-user\n"),
            [$"refs/heads/app/{plan.AppId}/{plan.Metadata.Architecture.PackagePrefix}/stable"] = ByteSource.FromString(commitChecksum),
            ["summary"] = ByteSource.FromBytes(Array.Empty<byte>())
        };

        // objects for blobs
        foreach (var b in blobs)
        {
            var content = new Blob(b.Value.source.Array()).Serialize();
            var chk = Sha256(content);
            var path = $"objects/{chk.Substring(0,2)}/{chk.Substring(2)}";
            repoFiles[path] = ByteSource.FromBytes(content);
        }

        // tree object
        repoFiles[$"objects/{treeChecksum[..2]}/{treeChecksum[2..]}"] = ByteSource.FromBytes(treeBytes);
        // commit object
        repoFiles[$"objects/{commitChecksum[..2]}/{commitChecksum[2..]}"] = ByteSource.FromBytes(commitBytes);

        return repoFiles.ToRootContainer();
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}

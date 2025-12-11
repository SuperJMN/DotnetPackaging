namespace DotnetPackaging.Flatpak;

internal static class OstreeEncoders
{
    // Placeholder encoders. Will be replaced by proper GVariant serialization.
    public static byte[] EncodeTree(IReadOnlyDictionary<string, string> entries)
    {
        // Scaffold using minimal GVariant-like layout: [name0\0sha0\0][name1\0sha1\0]...
        var gv = GVariant.Create();
        foreach (var kv in entries.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            gv.String(kv.Key).String(kv.Value);
        }
        return gv.ToArray();
    }

    public static byte[] EncodeCommit(string treeChecksum, string subject, DateTimeOffset timestamp)
    {
        // Scaffold using minimal GVariant-like layout
        var gv = GVariant.Create()
            .String(treeChecksum)
            .String(subject)
            .UInt64((ulong)timestamp.ToUnixTimeSeconds());
        return gv.ToArray();
    }
}

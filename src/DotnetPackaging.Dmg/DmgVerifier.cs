using CSharpFunctionalExtensions;
using DiscUtils.Iso9660;

namespace DotnetPackaging.Dmg;

public static class DmgVerifier
{
    public static Task<Result<string>> Verify(string dmgPath)
    {
        if (!File.Exists(dmgPath))
            return Task.FromResult(Result.Failure<string>("File not found"));

        // Try ISO/UDTO first
        try
        {
            using var fs = File.OpenRead(dmgPath);
            using var iso = new CDReader(fs, true);
            // touch the root to ensure it's a valid ISO
            _ = iso.GetDirectories("/");

            var apps = FindAppBundles(iso);
            if (apps.Count == 0)
                return Task.FromResult(Result.Failure<string>("No .app bundle found at image root or subfolders"));

            var details = new List<string>();
            foreach (var app in apps)
            {
                var hasContents = iso.DirectoryExists(app + "/Contents");
                var hasMacOS = iso.DirectoryExists(app + "/Contents/MacOS");
                var anyExec = iso.GetFiles(app + "/Contents/MacOS").Any();
                details.Add($"{app}: Contents={(hasContents ? "yes" : "no")}, MacOS={(hasMacOS ? "yes" : "no")}, ExecFiles={(anyExec ? "yes" : "no")}");
            }

            return Task.FromResult(Result.Success("ISO/UDTO DMG OK\n" + string.Join("\n", details)));
        }
        catch
        {
            // not ISO
        }

        // If DiscUtils path failed, try raw ISO9660 PVD signature (CD001)
        try
        {
            using var fs = File.OpenRead(dmgPath);
            if (IsIso9660(fs))
            {
                return Task.FromResult(Result.Success("ISO/UDTO DMG detected (content not enumerated)"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<string>($"Failed to inspect file: {ex.Message}"));
        }

        // Minimal UDIF detection: footer 'koly' in last 512 bytes
        try
        {
            using var fs = File.OpenRead(dmgPath);
            if (fs.Length >= 512)
            {
                fs.Seek(-512, SeekOrigin.End);
                Span<byte> buf = stackalloc byte[512];
                fs.Read(buf);
                if (buf.Slice(0, 4).SequenceEqual(new byte[] { (byte)'k', (byte)'o', (byte)'l', (byte)'y' }))
                {
                    return Task.FromResult(Result.Success("UDIF DMG with koly footer detected (detailed BLKX/plist validation not implemented)"));
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<string>($"Failed to inspect file: {ex.Message}"));
        }

        return Task.FromResult(Result.Failure<string>("Unknown or unsupported DMG container"));
    }

    private static bool IsIso9660(Stream s)
    {
        // PVD at sector 16 (0x8000); standard identifier 'CD001' at offset 1
        long[] sectors = new long[] { 16, 17, 18, 19 };
        foreach (var sec in sectors)
        {
            if (s.Length < (sec + 1) * 2048) break;
            s.Seek(sec * 2048 + 1, SeekOrigin.Begin);
            Span<byte> id = stackalloc byte[5];
            if (s.Read(id) == 5 && id.SequenceEqual(new byte[] { (byte)'C', (byte)'D', (byte)'0', (byte)'0', (byte)'1' }))
                return true;
        }
        return false;
    }

    private static List<string> FindAppBundles(CDReader iso)
    {
        var found = new List<string>();
        void Recurse(string path)
        {
            foreach (var d in iso.GetDirectories(path))
            {
                var name = System.IO.Path.GetFileName(d.TrimEnd('/', '\\'));
                var full = path == string.Empty ? d : (path.TrimEnd('/', '\\') + "/" + name);
                if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    found.Add("/" + full.TrimStart('/', '\\'));
                Recurse(full);
            }
        }
        Recurse("");
        return found;
    }
}

using CSharpFunctionalExtensions;

namespace DotnetPackaging.Dmg;

public static class DmgVerifier
{
    public static Task<Result<string>> Verify(string dmgPath)
    {
        if (!File.Exists(dmgPath))
            return Task.FromResult(Result.Failure<string>("File not found"));

        // Try ISO/UDTO first (requires DiscUtils.Iso9660 which is only in tests)
        // Skip for now as we're focused on UDIF format

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

        // UDIF detection: footer 'koly' in last 512 bytes
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
                    // Extract basic info from Koly block
                    var version = ReadBigEndianUInt32(buf.Slice(4, 4));
                    var flags = ReadBigEndianUInt32(buf.Slice(12, 4));
                    var sectorCount = ReadBigEndianUInt64(buf.Slice(492, 8));
                    
                    var compressionType = (flags & 1) != 0 ? "flattened" : "uncompressed";
                    return Task.FromResult(Result.Success($"UDIF DMG OK (version={version}, {compressionType}, sectors={sectorCount})"));
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

    private static uint ReadBigEndianUInt32(Span<byte> data)
    {
        return ((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
    }

    private static ulong ReadBigEndianUInt64(Span<byte> data)
    {
        return ((ulong)data[0] << 56) | ((ulong)data[1] << 48) | ((ulong)data[2] << 40) | ((ulong)data[3] << 32) |
               ((ulong)data[4] << 24) | ((ulong)data[5] << 16) | ((ulong)data[6] << 8) | data[7];
    }

}

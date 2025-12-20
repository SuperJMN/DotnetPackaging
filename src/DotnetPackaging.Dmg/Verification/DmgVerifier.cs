using System.Buffers.Binary;
using CSharpFunctionalExtensions;
using DotnetPackaging.Formats.Dmg.Udif;

namespace DotnetPackaging.Dmg.Verification;

public static class DmgVerifier
{
    public static async Task<Result<string>> Verify(string dmgPath)
    {
        if (!File.Exists(dmgPath))
            return Result.Failure<string>("File not found");

        // Try ISO/UDTO first (requires DiscUtils.Iso9660 which is only in tests)
        // Skip for now as we're focused on UDIF format

        // If DiscUtils path failed, try raw ISO9660 PVD signature (CD001)
        try
        {
            using var fs = File.OpenRead(dmgPath);
            if (IsIso9660(fs))
            {
                return Result.Success("ISO/UDTO DMG detected (content not enumerated)");
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Failed to inspect file: {ex.Message}");
        }

        try
        {
            var udif = await UdifImage.Load(dmgPath);
            var dataFork = await udif.ExtractDataFork();
            if (dataFork.Length < 1026)
            {
                return Result.Failure<string>("UDIF detected but payload is too small");
            }

            var hfsSignature = BinaryPrimitives.ReadUInt16BigEndian(dataFork.AsSpan(1024, 2));
            if (hfsSignature != 0x482B)
            {
                return Result.Failure<string>("UDIF detected but HFS+ signature not found");
            }

            return Result.Success($"UDIF DMG OK (runs={udif.Runs.Count}, sectors={udif.Trailer.SectorCount})");
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is IOException)
        {
            return Result.Failure<string>($"Failed to inspect file: {ex.Message}");
        }
    }

    private static bool IsIso9660(Stream s)
    {
        // PVD at sector 16 (0x8000); standard identifier 'CD001' at offset 1
        long[] sectors = new long[] { 16, 17, 18, 19 };
        Span<byte> id = stackalloc byte[5];
        foreach (var sec in sectors)
        {
            if (s.Length < (sec + 1) * 2048) break;
            s.Seek(sec * 2048 + 1, SeekOrigin.Begin);
            if (s.Read(id) == 5 && id.SequenceEqual(new byte[] { (byte)'C', (byte)'D', (byte)'0', (byte)'0', (byte)'1' }))
                return true;
        }
        return false;
    }
}

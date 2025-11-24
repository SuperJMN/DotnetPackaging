namespace DotnetPackaging.Deb.Tests;

internal static class ArArchiveReader
{
    private const string Magic = "!<arch>\n";
    private const int HeaderLength = 60;

    public static IReadOnlyList<ArEntry> Read(byte[] archive)
    {
        if (archive.Length < Magic.Length)
        {
            throw new InvalidDataException("AR archive is too small to contain the magic header.");
        }

        var signature = Encoding.ASCII.GetString(archive, 0, Magic.Length);
        if (!string.Equals(signature, Magic, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Invalid AR signature '{signature}'.");
        }

        var entries = new List<ArEntry>();
        var position = Magic.Length;

        while (position + HeaderLength <= archive.Length)
        {
            var header = archive.AsSpan(position, HeaderLength);
            var name = NormalizeName(ReadString(header[..16]));
            var size = ParseDecimal(ReadString(header.Slice(48, 10)));

            var dataStart = position + HeaderLength;
            var dataEnd = dataStart + size;
            if (dataEnd > archive.Length)
            {
                throw new InvalidDataException($"Entry '{name}' declares {size} bytes but archive ends earlier.");
            }

            var data = archive[dataStart..dataEnd];
            entries.Add(new ArEntry(name, data.ToArray()));

            // AR members are aligned to even byte boundaries.
            var alignedSize = size % 2 == 0 ? size : size + 1;
            position = dataStart + alignedSize;
        }

        return entries;
    }

    private static string NormalizeName(string rawName)
    {
        var trimmed = rawName.Trim();
        return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed[..^1] : trimmed;
    }

    private static string ReadString(ReadOnlySpan<byte> span) => Encoding.ASCII.GetString(span);

    private static int ParseDecimal(string value)
    {
        if (!int.TryParse(value.Trim(), out var parsed))
        {
            throw new InvalidDataException($"AR header contains an invalid size value '{value}'.");
        }

        return parsed;
    }
}

public sealed record ArEntry(string Name, byte[] Data);

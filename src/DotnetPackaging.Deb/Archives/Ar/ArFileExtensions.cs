using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Archives.Ar;

public static class ArFileExtensions
{
    private const string Signature = "!<arch>\n";

    public static IByteSource ToByteSource(this ArFile arFile)
    {
        if (arFile.Entries.All(entry => entry.Content.KnownLength().HasValue))
        {
            return arFile.ToStreamingByteSource();
        }

        return ByteSourceMaterializationExtensions.FromTempFileFactory(() => arFile.ToMaterializedByteSource());
    }

    private static IByteSource ToStreamingByteSource(this ArFile arFile)
    {
        var parts = new List<IByteSource>
        {
            Bytes(Encoding.ASCII.GetBytes(Signature))
        };

        foreach (var entry in arFile.Entries)
        {
            var contentLength = entry.Content.KnownLength().Value;
            parts.Add(Bytes(Encoding.ASCII.GetBytes(BuildHeader(entry, contentLength))));
            parts.Add(entry.Content);

            if (contentLength % 2 != 0)
            {
                parts.Add(Bytes([(byte)'\n']));
            }
        }

        return parts.ConcatWithLength();
    }

    private static IByteSource Bytes(byte[] bytes)
    {
        return ByteSource.FromBytes(bytes).WithLength(bytes.LongLength);
    }

    private static async Task<Result<MaterializedByteSourceFile>> ToMaterializedByteSource(this ArFile arFile)
    {
        var output = MaterializedByteSourceFile.Create(".ar");
        try
        {
            await using var stream = File.Open(output.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(Signature)).ConfigureAwait(false);

            foreach (var entry in arFile.Entries)
            {
                var content = await entry.Content.OpenReadWithLength(".ar-entry").ConfigureAwait(false);
                if (content.IsFailure)
                {
                    output.Dispose();
                    return Result.Failure<MaterializedByteSourceFile>($"Could not read ar entry '{entry.Name}': {content.Error}");
                }

                using var contentLease = content.Value;
                var header = BuildHeader(entry, contentLease.Length);
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header)).ConfigureAwait(false);
                await contentLease.Stream.CopyToAsync(stream).ConfigureAwait(false);

                if (contentLease.Length % 2 != 0)
                {
                    stream.WriteByte((byte)'\n');
                }
            }

            return output;
        }
        catch (Exception ex)
        {
            output.Dispose();
            return Result.Failure<MaterializedByteSourceFile>($"Could not write ar archive: {ex.Message}");
        }
    }

    private static string BuildHeader(ArEntry entry, long size)
    {
        var modeOctal = Convert.ToString(entry.Properties.Mode, 8);

        return
            $"{Field(entry.Name, 16)}" +
            $"{Field(entry.Properties.LastModification.ToUnixTimeSeconds().ToString(), 12)}" +
            $"{Field(entry.Properties.OwnerId.ToString(), 6)}" +
            $"{Field(entry.Properties.GroupId.ToString(), 6)}" +
            $"{Field($"100{modeOctal}", 8)}" +
            $"{Field(size.ToString(), 10)}" +
            "`\n";
    }

    private static string Field(string value, int width)
    {
        if (value.Length > width)
        {
            return value[..width];
        }

        return value.PadRight(width);
    }
}

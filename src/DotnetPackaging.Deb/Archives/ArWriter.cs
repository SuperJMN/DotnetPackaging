using System.Text;
using MoreLinq;

namespace DotnetPackaging.Deb.Archives;

public class ArWriter : IArFileVisitor
{
    private readonly Stream stream;

    public ArWriter(Stream stream)
    {
        this.stream = stream;
    }
    
    public async Task Visit(ArFile arFile)
    {
        await stream.WriteAsync("!<arch>\n".GetAsciiBytes());
        arFile.Entries.ForEach(e => e.Accept(this));
    }

    public async Task Visit(Entry entry)
    {
        await stream.WriteAsync(entry.Name.PadRight(16).GetAsciiBytes());
        entry.Properties.Accept(this);
        await using var contentStream = entry.ContentStreamFactory();
        await WritePaddedString(contentStream.Length.ToString(), 10);
        await stream.WriteAsync("`\n".GetAsciiBytes());
    }

    public async Task Visit(Properties properties)
    {
        await WritePaddedString(properties.LastModification.ToUnixTimeSeconds().ToString(), 12);
        await WritePaddedString(properties.OwnerId.GetValueOrDefault().ToString(), 6);
        await WritePaddedString(properties.GroupId.GetValueOrDefault().ToString(), 6);
        await WritePaddedString("100" + properties.FileMode, 8);
    }
    
    private async Task WritePaddedString(string str, int length)
    {
        str = str.PadRight(length);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(str));
    }
}
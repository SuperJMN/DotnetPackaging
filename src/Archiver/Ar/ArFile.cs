using System.Reactive.Linq;
using Zafiro.IO;

namespace Archiver.Ar;

public class ArFile
{
    private readonly Stream output;

    public ArFile(Stream output)
    {
        this.output = output;
    }

    public async Task Build(params EntryData[] entries)
    {
        var arContents = entries
            .Select(entry => new Entry(entry).Bytes)
            .Concat();

        await Signature.Concat(arContents).DumpTo(output);
    }

    private IObservable<byte> Signature => "!<arch>\n".GetAsciiBytes().ToObservable();
}
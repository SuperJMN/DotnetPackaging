using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class Contents
{
    private readonly Dictionary<ZafiroPath, Func<IObservable<byte>>> dictionary;

    public Contents(Dictionary<ZafiroPath, Func<IObservable<byte>>> dictionary)
    {
        this.dictionary = dictionary;
    }

    public IEnumerable<(ZafiroPath, Func<IObservable<byte>>)> Entries => dictionary.AsEnumerable().Select(a => (a.Key, a.Value));
}
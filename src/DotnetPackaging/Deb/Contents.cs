using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public class Contents
{
    private readonly Dictionary<ZafiroPath, Content> dictionary;

    public Contents(Dictionary<ZafiroPath, Content> dictionary)
    {
        this.dictionary = dictionary;
    }

    public IEnumerable<(ZafiroPath, Content)> Entries => dictionary.AsEnumerable().Select(a => (a.Key, a.Value));
}
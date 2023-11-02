using CSharpFunctionalExtensions;

namespace DotnetPackaging.Deb;

public class IconResources
{
    private readonly Dictionary<int, IconData> iconsDatas;

    private IconResources(Dictionary<int, IconData> iconsDatas)
    {
        this.iconsDatas = iconsDatas;
    }

    public IEnumerable<(int, IconData)> Icons => iconsDatas.Select(pair => (pair.Key, pair.Value));

    public static Result<IconResources> Create(params (int, IconData)[] iconsDatas)
    {
        var dic = iconsDatas.ToDictionary(data => data.Item1, data => data.Item2);

        return new IconResources(dic);
    }
}
using CSharpFunctionalExtensions;

namespace DotnetPackaging.New.Deb;

public class IconResources
{
    private readonly Dictionary<int, IconData> iconsDatas;

    private IconResources(Dictionary<int, IconData> iconsDatas)
    {
        this.iconsDatas = iconsDatas;
    }

    public IEnumerable<IconData> Icons => iconsDatas.Values;

    public static Result<IconResources> Create(params IconData[] iconsDatas)
    {
        var dic = iconsDatas.ToDictionary(data => data.TargetSize, data => data);

        return new IconResources(dic);
    }
}
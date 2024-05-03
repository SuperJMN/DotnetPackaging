using CSharpFunctionalExtensions;
using SixLabors.ImageSharp;
using Zafiro.FileSystem;

namespace DotnetPackaging;

public static class IconMixin
{
    public static Task<Result<Image>> FromData(this IData data)
    {
        var memoryStream = new MemoryStream();
        
        return data.DumpTo(memoryStream)
            .Map(() => Image.Load(memoryStream.ToArray()));
    }
}
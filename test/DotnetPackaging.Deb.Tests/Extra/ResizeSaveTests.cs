using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace DotnetPackaging.Tests.Extra;

public class ResizeSaveTests
{
    [Fact]
    public async Task Resize()
    {
        await using var stream = File.OpenRead("Tar\\TestFiles\\icon.png");
        using var image = await Image.LoadAsync(stream);
        image.Mutate(c => c.Resize(64, 64));
        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Ohsí.png");
        await image.SaveAsPngAsync(output);
    }
}
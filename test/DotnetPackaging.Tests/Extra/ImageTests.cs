using System.Reactive.Linq;
using DotnetPackaging.Deb;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Extra;

public class ImageTests
{
    [Fact]
    public async Task Image_test()
    {
        var iconData = new IconData(64, () =>
        {
            return Observable.Using(() => File.OpenRead("Tar\\TestFiles\\icon.png"), stream => stream.ToObservable());
        });

        await using var output = File.Create("C:\\Users\\JMN\\Desktop\\Testing\\resizedicon.png");
        await iconData.IconBytes().DumpTo(output);
    }
}
using ClassLibrary1;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task CreateAppImage()
    {
        // TODO:
        //var stream = new MemoryStream();
        //var build = new AppImageBuilder().Build(new InMemoryDataTree("", new List<IData>(), new List<IDataTree>()));
        //var writeResult = await build.Bind(image => AppImageWriter.Write(stream, image));
        //writeResult.Should().Succeed();
    }
}
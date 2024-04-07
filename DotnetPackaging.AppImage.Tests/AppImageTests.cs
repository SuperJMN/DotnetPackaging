using ClassLibrary1;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public async Task CreateAppImage()
    {
    
        var stream = new MemoryStream();
        var inMemoryBlobContainer = new InMemoryBlobContainer("", new List<IBlob>(), new List<IBlobContainer>());
        var build = new AppImageBuilder().Build(inMemoryBlobContainer, new TestRuntime(), new DefaultScriptAppRun("/usr/bin/Blabla"));
        var writeResult = await build.Bind(image => AppImageWriter.Write(stream, image));
        writeResult.Should().Succeed();
    }
}
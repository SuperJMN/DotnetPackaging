using FluentAssertions;

namespace DotnetPackaging.Tests.Deb.EndToEnd;

public class DebCreationTests
{
    [Fact]
    public async Task Local_deb_builder()
    {
        var result  = await Create.Deb(
            await TestData.GetPackageDefinition(),
            contentsPath: @"TestFiles\Content", 
            outputPathForDebFile: @"C:\Users\JMN\Desktop\Testing\SampleOther.deb");

        result.Should().Succeed();
    }
}
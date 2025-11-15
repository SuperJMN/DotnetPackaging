using CSharpFunctionalExtensions;
using DotnetPackaging.Publish;
using FluentAssertions;

namespace DotnetPackaging.Tests.Publish;

public class MsBuildVersionPropertiesFactoryTests
{
    [Theory]
    [InlineData("3.1.0", "3.1.0.0")]
    [InlineData("3.1.0-alpha.1+sha", "3.1.0.0")]
    public void Uses_nuget_version_information_when_normalizing(string input, string expected)
    {
        var properties = MsBuildVersionPropertiesFactory.Create(Maybe<string>.From(input));

        properties.Should().NotBeNull();
        var dictionary = properties!;

        dictionary.Should().ContainKey("AssemblyVersion");
        dictionary["AssemblyVersion"].Should().Be(expected);
        dictionary.Should().ContainKey("FileVersion");
        dictionary["FileVersion"].Should().Be(expected);
    }

    [Fact]
    public void Discards_values_with_components_out_of_range()
    {
        var properties = MsBuildVersionPropertiesFactory.Create(Maybe<string>.From("70000.0.0"));

        properties.Should().NotBeNull();
        var dictionary = properties!;

        dictionary.Should().NotContainKey("AssemblyVersion");
        dictionary.Should().ContainKey("Version");
        dictionary["Version"].Should().Be("70000.0.0");
    }

    [Fact]
    public void Falls_back_to_numeric_prefix_when_nuget_parsing_fails()
    {
        var properties = MsBuildVersionPropertiesFactory.Create(Maybe<string>.From("2024.03.17-preview"));

        properties.Should().NotBeNull();
        var dictionary = properties!;

        dictionary.Should().ContainKey("AssemblyVersion");
        dictionary["AssemblyVersion"].Should().Be("2024.3.17.0");
    }
}

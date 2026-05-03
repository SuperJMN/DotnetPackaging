using Serilog.Core;

namespace DotnetPackaging.Deb.Tests;

public class ProjectPackagingContextTests
{
    [Fact]
    public void FromProject_ShouldResolvePackagingDefaultsFromProjectMetadata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dotnetpackaging-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var projectPath = Path.Combine(tempDir, "SampleApp.csproj");

        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <AssemblyName>sample-runner</AssemblyName>
                <Product>Sample Product</Product>
                <Company>Sample Company</Company>
                <Description>Sample description</Description>
                <Authors>Sample Authors</Authors>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
                <PackageProjectUrl>https://example.com/sample</PackageProjectUrl>
              </PropertyGroup>
            </Project>
            """);

        try
        {
            var result = ProjectPackagingContext.FromProject(projectPath, Logger.None);

            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : "");
            var options = result.Value.ResolveFromDirectoryOptions(new FromDirectoryOptions());

            options.ExecutableName.GetValueOrDefault().Should().Be("sample-runner");
            options.Description.GetValueOrDefault().Should().Be("Sample description");
            options.Maintainer.GetValueOrDefault().Should().Be("Sample Authors");
            options.Vendor.GetValueOrDefault().Should().Be("Sample Company");
            options.License.GetValueOrDefault().Should().Be("MIT");
            options.Url.GetValueOrDefault()?.ToString().Should().Be("https://example.com/sample");
            options.IsTerminal.GetValueOrDefault().Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}

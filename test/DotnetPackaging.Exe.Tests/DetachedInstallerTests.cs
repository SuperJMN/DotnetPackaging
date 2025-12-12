using System;
using System.IO;
using CSharpFunctionalExtensions;
using DotnetPackaging.Exe;
using FluentAssertions;
using Xunit;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using System.IO.Abstractions;
using IOPath = System.IO.Path;

namespace DotnetPackaging.Exe.Tests;

public class DetachedInstallerTests
{
    [Fact]
    public async Task Installer_can_be_written_after_publish_directory_is_deleted()
    {
        var tempRoot = IOPath.Combine(IOPath.GetTempPath(), $"dp-exe-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        var exePath = IOPath.Combine(tempRoot, "App.exe");
        await File.WriteAllTextAsync(exePath, "installer payload");

        var fs = new FileSystem();
        var publishContainer = new DirectoryContainer(fs.DirectoryInfo.New(tempRoot)).AsRoot();
        var options = new Options
        {
            Name = Maybe<string>.From("App"),
            Version = Maybe<string>.From("1.0.0"),
        };

        var service = new ExePackagingService();
        var stub = ByteSource.FromString("stub-bytes");
        var result = await service.BuildFromDirectory(
            publishContainer,
            "App-setup.exe",
            options,
            vendor: "TestVendor",
            runtimeIdentifier: "win-x64",
            stubFile: stub,
            setupLogo: null);

        result.IsSuccess.Should().BeTrue();
        using var package = result.Value;
        Directory.Delete(tempRoot, true);

        var outputPath = IOPath.Combine(IOPath.GetTempPath(), $"dp-exe-installer-{Guid.NewGuid():N}.exe");
        var writeResult = await package.WriteTo(outputPath);

        writeResult.IsSuccess.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
        File.Delete(outputPath);
    }
}

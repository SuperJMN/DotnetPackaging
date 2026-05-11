using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Metadata;
using FluentAssertions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageDesktopMetadataTests
{
    [Fact]
    public async Task CreateMetadata_strips_desktop_host_suffix_from_implicit_project_identity()
    {
        var root = CreateApplicationRoot("TestApp.Desktop");
        var executable = root.ResourcesWithPathsRecursive().Single();
        var options = new FromDirectoryOptions()
            .WithProjectMetadata(new ProjectMetadata(
                Product: Maybe.From("TestApp.Desktop"),
                Company: Maybe<string>.None,
                Description: Maybe<string>.None,
                Authors: Maybe<string>.None,
                Copyright: Maybe<string>.None,
                PackageLicenseExpression: Maybe<string>.None,
                PackageProjectUrl: Maybe<string>.None,
                PackageId: Maybe<string>.None,
                Version: Maybe<string>.None,
                RepositoryUrl: Maybe<string>.None,
                AssemblyName: Maybe.From("TestApp.Desktop"),
                AssemblyTitle: Maybe<string>.None,
                OutputType: Maybe.From("WinExe")));

        var metadata = await BuildUtils.CreateMetadata(
            options,
            root,
            Architecture.X64,
            executable,
            isTerminal: false,
            containerName: Maybe<string>.None);

        metadata.Name.Should().Be("TestApp");
        metadata.Package.Should().Be("testapp");
        metadata.StartupWmClass.GetValueOrDefault().Should().Be("TestApp");
    }

    [Fact]
    public async Task BuildPlan_writes_startup_wm_class_with_desktop_entry_key_casing()
    {
        var root = CreateApplicationRoot("TestApp");
        var metadata = new AppImageMetadata("com.example.testapp", "TestApp", "testapp")
        {
            StartupWmClass = Maybe.From("TestApp")
        };

        var planResult = await new AppImageFactory().BuildPlan(root, metadata);

        planResult.Should().Succeed();
        var desktopContent = await ReadAllText(Find(planResult.Value, metadata.DesktopFileName));
        desktopContent.Should().Contain("StartupWMClass=TestApp");
        desktopContent.Should().NotContain("StartupWmClass=TestApp");
    }

    [Fact]
    public void ToAppImageMetadata_preserves_startup_wm_class()
    {
        var packageMetadata = new PackageMetadata("TestApp", Architecture.X64, false, "testapp", "1.0.0")
        {
            Name = "TestApp",
            Architecture = Architecture.X64,
            Package = "testapp",
            Version = "1.0.0",
            StartupWmClass = Maybe.From("TestApp"),
            ModificationTime = DateTimeOffset.UnixEpoch
        };

        var appImageMetadata = AppImagePackager.ToAppImageMetadata(packageMetadata);

        appImageMetadata.StartupWmClass.GetValueOrDefault().Should().Be("TestApp");
    }

    private static RootContainer CreateApplicationRoot(string executableName)
    {
        var files = new Dictionary<string, IByteSource>
        {
            [executableName] = ByteSource.FromBytes(CreateElfBytes())
        };

        var rootResult = files.ToRootContainer();
        rootResult.Should().Succeed();
        return rootResult.Value;
    }

    private static byte[] CreateElfBytes()
    {
        var bytes = new byte[32];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';

        var executableType = BitConverter.GetBytes((short)2);
        Array.Copy(executableType, 0, bytes, 16, executableType.Length);

        return bytes;
    }

    private static INamedByteSource Find(AppImageBuildPlan plan, string path)
    {
        return plan.ToRootContainer().ResourcesWithPathsRecursive()
            .First(x => string.Equals(((INamedWithPath)x).FullPath().ToString(), path, StringComparison.Ordinal));
    }

    private static async Task<string> ReadAllText(INamedByteSource source)
    {
        await using var stream = source.ToStreamSeekable();
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}

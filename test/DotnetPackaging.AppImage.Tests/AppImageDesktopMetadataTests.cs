using CSharpFunctionalExtensions;
using DotnetProjectKit;
using DotnetPackaging.AppImage.Metadata;
using FluentAssertions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageDesktopMetadataTests
{
    [Fact]
    public async Task CreateMetadata_keeps_desktop_host_startup_wm_class_from_project_identity()
    {
        var root = CreateApplicationRoot("TestApp.Desktop");
        var executable = root.ResourcesWithPathsRecursive().Single();
        var options = new FromDirectoryOptions()
            .WithApplicationInfo(CreateApplicationInfo(
                assemblyName: "TestApp.Desktop",
                displayName: "TestApp",
                packageName: "TestApp",
                startupWmClass: "TestApp.Desktop"));

        var metadata = await BuildUtils.CreateMetadata(
            options,
            root,
            Architecture.X64,
            executable,
            isTerminal: false,
            containerName: Maybe<string>.None);

        metadata.Name.Should().Be("TestApp");
        metadata.Package.Should().Be("testapp");
        metadata.StartupWmClass.GetValueOrDefault().Should().Be("TestApp.Desktop");
    }

    [Fact]
    public async Task CreateMetadata_uses_executable_name_as_default_startup_wm_class()
    {
        var root = CreateApplicationRoot("TestApp.Desktop");
        var executable = root.ResourcesWithPathsRecursive().Single();

        var metadata = await BuildUtils.CreateMetadata(
            new FromDirectoryOptions(),
            root,
            Architecture.X64,
            executable,
            isTerminal: false,
            containerName: Maybe<string>.None);

        metadata.Name.Should().Be("TestApp");
        metadata.Package.Should().Be("testapp");
        metadata.StartupWmClass.GetValueOrDefault().Should().Be("TestApp.Desktop");
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
    public async Task BuildPlan_writes_comment_and_appstream_description()
    {
        var root = CreateApplicationRoot("TestApp");
        var metadata = new AppImageMetadata("com.example.testapp", "TestApp", "testapp")
        {
            Summary = Maybe.From("Short test summary"),
            Comment = Maybe.From("Desktop test comment"),
            Description = Maybe.From("Long test description")
        };

        var planResult = await new AppImageFactory().BuildPlan(root, metadata);

        planResult.Should().Succeed();
        var desktopContent = await ReadAllText(Find(planResult.Value, metadata.DesktopFileName));
        desktopContent.Should().Contain("Comment=Desktop test comment");

        var appStreamContent = await ReadAllText(Find(planResult.Value, $"usr/share/metainfo/{metadata.AppDataFileName}"));
        appStreamContent.Should().Contain("<summary>Short test summary</summary>");
        appStreamContent.Should().Contain("<description>");
        appStreamContent.Should().Contain("<p>Long test description</p>");
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

    [Fact]
    public void ToAppImageMetadata_prefers_comment_for_desktop_entry_and_preserves_description_for_appstream()
    {
        var packageMetadata = new PackageMetadata("TestApp", Architecture.X64, false, "testapp", "1.0.0")
        {
            Name = "TestApp",
            Architecture = Architecture.X64,
            Package = "testapp",
            Version = "1.0.0",
            Comment = Maybe.From("Desktop comment"),
            Description = Maybe.From("AppStream description"),
            ModificationTime = DateTimeOffset.UnixEpoch
        };

        var appImageMetadata = AppImagePackager.ToAppImageMetadata(packageMetadata);

        appImageMetadata.Comment.GetValueOrDefault().Should().Be("Desktop comment");
        appImageMetadata.Description.GetValueOrDefault().Should().Be("AppStream description");
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

    private static ApplicationInfo CreateApplicationInfo(
        string assemblyName,
        string displayName,
        string packageName,
        string startupWmClass)
    {
        return new ApplicationInfo
        {
            ProjectPath = "TestApp.Desktop.csproj",
            Metadata = ProjectMetadata.FromValues(new Dictionary<string, string>
            {
                ["AssemblyName"] = assemblyName,
                ["Product"] = assemblyName,
                ["OutputType"] = "WinExe"
            }),
            AssemblyName = new ResolvedValue<string>(assemblyName, ApplicationInfoSource.Msbuild),
            ExecutableName = new ResolvedValue<string>(assemblyName, ApplicationInfoSource.Msbuild),
            DisplayName = new ResolvedValue<string>(displayName, ApplicationInfoSource.Convention),
            PackageName = new ResolvedValue<string>(packageName, ApplicationInfoSource.Convention),
            Version = new ResolvedValue<string>("1.0.0", ApplicationInfoSource.Default),
            StartupWmClass = new ResolvedValue<string>(startupWmClass, ApplicationInfoSource.Convention),
            OutputType = new ResolvedValue<string>("WinExe", ApplicationInfoSource.Msbuild)
        };
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

using System.Text;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Flatpak.Tests;

public sealed class FlatpakFactoryTests
{
    [Fact]
    public async Task BuildPlan_includes_metadata_file()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var files = EnumeratePaths(plan);

        files.Should().Contain("metadata");
    }

    [Fact]
    public async Task BuildPlan_includes_desktop_file()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var files = EnumeratePaths(plan);

        files.Should().Contain(path => path.Contains(".desktop"));
    }

    [Fact]
    public async Task BuildPlan_includes_metainfo_file()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var files = EnumeratePaths(plan);

        files.Should().Contain(path => path.Contains(".metainfo.xml"));
    }

    [Fact]
    public async Task BuildPlan_includes_wrapper_script_in_bin()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var files = EnumeratePaths(plan);

        files.Should().Contain(path => path.StartsWith("files/bin/"));
    }

    [Fact]
    public async Task BuildPlan_metadata_contains_application_section()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var metadataContent = await ReadFileContent(plan, "metadata");

        metadataContent.Should().Contain("[Application]");
        metadataContent.Should().Contain("name=com.test.sample");
    }

    [Fact]
    public async Task BuildPlan_metadata_contains_context_section()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var metadataContent = await ReadFileContent(plan, "metadata");

        metadataContent.Should().Contain("[Context]");
    }

    [Fact]
    public async Task BuildPlan_uses_correct_appId()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;

        plan.AppId.Should().Be("com.test.sample");
    }

    [Fact]
    public async Task BuildPlan_respects_options_for_runtime()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var options = new FlatpakOptions
        {
            Runtime = "org.kde.Platform",
            Sdk = "org.kde.Sdk",
            RuntimeVersion = "6.5"
        };
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata, options);

        result.Should().Succeed();
        var plan = result.Value;
        var metadataContent = await ReadFileContent(plan, "metadata");

        metadataContent.Should().Contain("runtime=org.kde.Platform");
        metadataContent.Should().Contain("sdk=org.kde.Sdk");
    }

    [Fact]
    public async Task BuildPlan_includes_application_files_under_files_directory()
    {
        var root = CreateApplicationRoot();
        var metadata = CreateTestMetadata();
        var factory = new FlatpakFactory();

        var result = await factory.BuildPlan(root, metadata);

        result.Should().Succeed();
        var plan = result.Value;
        var files = EnumeratePaths(plan);

        files.Should().Contain(path => path.StartsWith("files/") && path.Contains("SampleApp"));
    }

    private static RootContainer CreateApplicationRoot()
    {
        var files = new Dictionary<string, IByteSource>
        {
            ["SampleApp"] = ByteSource.FromBytes(CreateElfBytes()),
            ["config.json"] = ByteSource.FromString("{\"key\":\"value\"}"),
        };

        var rootResult = files.ToRootContainer();
        rootResult.Should().Succeed();
        return rootResult.Value;
    }

    private static PackageMetadata CreateTestMetadata()
    {
        return new PackageMetadata("Sample App", Architecture.X64, false, "SampleApp", "1.0.0")
        {
            Name = "Sample App",
            Architecture = Architecture.X64,
            Package = "SampleApp",
            Version = "1.0.0",
            Id = Maybe<string>.From("com.test.sample"),
            ModificationTime = DateTimeOffset.UtcNow
        };
    }

    private static byte[] CreateElfBytes()
    {
        var bytes = new byte[32];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        bytes[4] = 2; // 64-bit
        bytes[5] = 1; // little endian

        var executableType = BitConverter.GetBytes((short)2);
        Array.Copy(executableType, 0, bytes, 16, executableType.Length);

        var machine = BitConverter.GetBytes((short)0x3E);
        Array.Copy(machine, 0, bytes, 18, machine.Length);

        return bytes;
    }

    private static IReadOnlyCollection<string> EnumeratePaths(FlatpakBuildPlan plan)
    {
        return plan.ToRootContainer().ResourcesWithPathsRecursive()
            .Select(x => ((INamedWithPath)x).FullPath().ToString())
            .ToList();
    }

    private static async Task<string> ReadFileContent(FlatpakBuildPlan plan, string path)
    {
        var file = plan.ToRootContainer().ResourcesWithPathsRecursive()
            .FirstOrDefault(x => ((INamedWithPath)x).FullPath().ToString() == path);

        if (file == null)
        {
            return string.Empty;
        }

        await using var stream = file.ToStreamSeekable();
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}

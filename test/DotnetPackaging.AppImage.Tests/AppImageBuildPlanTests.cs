using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Metadata;
using FluentAssertions;
using Zafiro.DivineBytes;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageBuildPlanTests
{
    [Fact]
    public async Task BuildPlan_includes_expected_artifacts()
    {
        var root = CreateApplicationRoot();
        var metadata = new AppImageMetadata("com.example.sample", "Sample App", "sample-app");
        var options = new AppImageOptions
        {
            IconNameOverride = Maybe<string>.From("custom-icon"),
        };

        var factory = new AppImageFactory();

        var planResult = await factory.BuildPlan(root, metadata, options);

        planResult.Should().Succeed();
        var plan = planResult.Value;

        plan.ExecutableName.Should().Be("SampleApp");
        plan.ExecutableTargetPath.Should().Be("/usr/bin/SampleApp");
        plan.IconName.Should().Be("custom-icon");
        plan.Metadata.Should().BeSameAs(metadata);

        var appDirPaths = Enumerate(plan);
        appDirPaths.Should().Contain(new[]
        {
            "AppRun",
            metadata.DesktopFileName,
            $"usr/share/metainfo/{metadata.AppDataFileName}",
            "usr/bin/SampleApp",
            "usr/bin/config.json",
            $"usr/share/icons/hicolor/scalable/apps/{plan.IconName}.svg",
            $"usr/share/icons/hicolor/256x256/apps/{plan.IconName}.png",
            ".DirIcon",
        });

        var desktopContent = await ReadAllText(Find(plan, metadata.DesktopFileName));
        desktopContent.Should().Contain("Exec=\"/usr/bin/SampleApp\"");

        var appRunContent = await ReadAllText(Find(plan, "AppRun"));
        appRunContent.Should().Contain("$APPDIR/usr/bin/SampleApp");

        plan.ToRootContainer().Should().BeOfType<RootContainer>();
    }

    private static RootContainer CreateApplicationRoot()
    {
        var files = new Dictionary<string, IByteSource>
        {
            ["SampleApp"] = ByteSource.FromBytes(CreateElfBytes()),
            ["config.json"] = ByteSource.FromString("{\"key\":\"value-with-length\"}"),
            ["icon.svg"] = ByteSource.FromString("<svg>minimal-icon-content</svg>"),
            ["icon.png"] = ByteSource.FromBytes(CreateFilledBytes(64, 0x42)),
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

    private static byte[] CreateFilledBytes(int length, byte value)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static INamedByteSource Find(AppImageBuildPlan plan, string path)
    {
        return plan.ToRootContainer().ResourcesWithPathsRecursive()
            .First(x => string.Equals(((INamedWithPath)x).FullPath().ToString(), path, StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<string> Enumerate(AppImageBuildPlan plan)
    {
        return plan.ToRootContainer().ResourcesWithPathsRecursive()
            .Select(x => ((INamedWithPath)x).FullPath().ToString())
            .ToList();
    }

    private static async Task<string> ReadAllText(INamedByteSource source)
    {
        await using var stream = source.ToStreamSeekable();
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}

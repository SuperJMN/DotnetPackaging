using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageEndToEndTests
{
    [Fact]
    public async Task Create_produces_appimage_bytes()
    {
        var root = Helpers.CreateApplicationRoot();
        var metadata = new AppImageMetadata("com.example.sample", "Sample App", "sample-app");
        var options = new AppImageOptions
        {
            IconNameOverride = Maybe<string>.From("custom-icon"),
        };

        var runtimeProvider = new FakeRuntimeProvider();
        var factory = new AppImageFactory(runtimeProvider);

        var result = await factory.Create(root, metadata, options);

        result.Should().Succeed();
        var container = result.Value;

        container.Runtime.Architecture.Should().Be(Architecture.X64);
        ReadAllText(container.Runtime).Should().Be("FAKE_RUNTIME");

        var paths = EnumeratePaths(container.Container).ToList();
        paths.Should().Contain(new[]
        {
            "AppRun",
            metadata.DesktopFileName,
            $"usr/bin/SampleApp",
            "usr/bin/config.json",
            $"usr/share/icons/hicolor/scalable/apps/{options.IconNameOverride.GetValueOrDefault()}.svg",
            $"usr/share/icons/hicolor/256x256/apps/{options.IconNameOverride.GetValueOrDefault()}.png",
            ".DirIcon",
        });

        var appImageBytesResult = await container.ToByteSource();
        appImageBytesResult.Should().Succeed();
        var appImageBytes = appImageBytesResult.Value.Array();
        appImageBytes.Length.Should().BeGreaterThan(0);
    }

    private static IEnumerable<string> EnumeratePaths(UnixDirectory directory, string prefix = "")
    {
        foreach (var file in directory.Files)
        {
            var filePath = string.IsNullOrEmpty(prefix) ? file.Name : $"{prefix}/{file.Name}";
            yield return filePath;
        }

        foreach (var subDir in directory.Subdirectories)
        {
            var nextPrefix = string.IsNullOrEmpty(prefix) ? subDir.Name : $"{prefix}/{subDir.Name}";
            foreach (var path in EnumeratePaths(subDir, nextPrefix))
            {
                yield return path;
            }
        }
    }

    private static string ReadAllText(IByteSource source)
    {
        using var stream = source.ToStreamSeekable();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        return reader.ReadToEnd();
    }

    private sealed class FakeRuntimeProvider : IRuntimeProvider
    {
        private readonly IRuntime runtime = new Runtime(ByteSource.FromString("FAKE_RUNTIME"), Architecture.X64);

        public Task<Result<IRuntime>> Create(Architecture architecture)
        {
            if (!ReferenceEquals(architecture, Architecture.X64))
            {
                return Task.FromResult(Result.Failure<IRuntime>("Unexpected architecture"));
            }

            return Task.FromResult(Result.Success(runtime));
        }
    }

    private static class Helpers
    {
        public static RootContainer CreateApplicationRoot()
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

            bytes[4] = 2; // 64-bit
            bytes[5] = 1; // little endian

            var executableType = BitConverter.GetBytes((short)2);
            Array.Copy(executableType, 0, bytes, 16, executableType.Length);

            var machine = BitConverter.GetBytes((short)0x3E);
            Array.Copy(machine, 0, bytes, 18, machine.Length);

            return bytes;
        }

        private static byte[] CreateFilledBytes(int length, byte value)
        {
            var bytes = new byte[length];
            Array.Fill(bytes, value);
            return bytes;
        }
    }
}

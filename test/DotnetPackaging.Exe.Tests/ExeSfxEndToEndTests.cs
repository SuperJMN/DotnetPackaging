using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DotnetPackaging.Exe;
using FluentAssertions;
using Xunit;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe.E2E.Tests;

public class ExeSfxEndToEndTests
{
    [Fact]
    public async Task From_project_builds_setup_with_expected_metadata_when_external_sample_is_available()
    {
        // Arrange: external sample project (skip test if not present)
        var projectPath = @"F:\\Repos\\Zafiro.Avalonia\\samples\\TestApp\\TestApp.Desktop\\TestApp.Desktop.csproj";
        if (!File.Exists(projectPath))
        {
            return; // skip silently if sample project is not available
        }

        var tmpDir = Directory.CreateTempSubdirectory("dp-exe-test-").FullName;
        var outputExe = Path.Combine(tmpDir, "Setup.exe");
        var metadataJsonPath = Path.Combine(tmpDir, "metadata.json");

        // Act: build the SFX installer from the project
        var service = new ExePackagingService();
        var result = await service.BuildFromProject(new FileInfo(projectPath), "win-x64", true, "Release", true, false, new FileInfo(outputExe), new Options(), vendor: null, stubFile: null, setupLogo: null);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);

        await result.Value.WriteTo(tmpDir);

        // Run the produced installer with the env hook to dump metadata and exit
        var psi = new ProcessStartInfo
        {
            FileName = outputExe,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment["DP_DUMP_METADATA_JSON"] = metadataJsonPath;
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();

        // Assert: metadata.json exists and has expected ApplicationName from project
        File.Exists(metadataJsonPath).Should().BeTrue("installer should dump metadata to the specified path");
        var json = await File.ReadAllTextAsync(metadataJsonPath);
        json.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("ApplicationName", out var appNameProp).Should().BeTrue("metadata should include ApplicationName");
        appNameProp.GetString().Should().Be("TestApp.Desktop");
    }
}

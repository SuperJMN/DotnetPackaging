using System.Text;
using DiscUtils.Iso9660;
using FluentAssertions;

namespace DotnetPackaging.Dmg.Tests;

public class DmgIsoBuilderTests
{
    [Fact]
    public async Task Creates_dmg_with_app_bundle_and_files()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        // Create minimal .app bundle
        var appDir = Path.Combine(publish, "MyApp.app", "Contents", "MacOS");
        Directory.CreateDirectory(appDir);
        var exePath = Path.Combine(appDir, "MyApp");
        await File.WriteAllTextAsync(exePath, "#!/bin/sh\necho Hello\n");

        // Optional adornments
        Directory.CreateDirectory(Path.Combine(publish, ".background"));
        await File.WriteAllTextAsync(Path.Combine(publish, ".background", "background.png"), "png");
        await File.WriteAllBytesAsync(Path.Combine(publish, ".VolumeIcon.icns"), new byte[]{0,1,2});

        var outDmg = Path.Combine(tempRoot.Path, "MyApp.dmg");
        await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "My App");

        File.Exists(outDmg).Should().BeTrue("the dmg file must be created");

        // Validate ISO/UDF contents (we wrote an ISO9660+Joliet stream)
        using var fs = File.OpenRead(outDmg);
        using var iso = new CDReader(fs, true);
        FileExistsAny(iso, new[]{"MyApp.app/Contents/MacOS/MyApp","MyApp.app\\Contents\\MacOS\\MyApp"}).Should().BeTrue();
        FileExistsAny(iso, new[]{".VolumeIcon.icns","\\.VolumeIcon.icns","/.VolumeIcon.icns"}).Should().BeTrue();
        DirExistsAny(iso, new[]{".background","\\.background","/.background"}).Should().BeTrue();
    }

    [Fact]
    public async Task Wraps_publish_output_into_app_bundle_when_missing()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");
        await File.WriteAllTextAsync(Path.Combine(publish, "Angor.deps.json"), "deps");
        await File.WriteAllTextAsync(Path.Combine(publish, "AppIcon.icns"), "icon");

        var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
        await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "Angor Avalonia");

        using var fs = File.OpenRead(outDmg);
        using var iso = new CDReader(fs, true);

        FileExistsAny(iso, new[]{"AngorAvalonia.app/Contents/MacOS/Angor","/AngorAvalonia.app/Contents/MacOS/Angor"}).Should().BeTrue();
        FileExistsAny(iso, new[]{"AngorAvalonia.app/Contents/Resources/AppIcon.icns","/AngorAvalonia.app/Contents/Resources/AppIcon.icns"}).Should().BeTrue();
        FileExistsAny(iso, new[]{"Angor","/Angor","\\Angor"}).Should().BeFalse("payload files must live inside the .app bundle only");

        var plistPath = FirstExistingPath(iso, new[]{"AngorAvalonia.app/Contents/Info.plist", "/AngorAvalonia.app/Contents/Info.plist", "\\AngorAvalonia.app\\Contents\\Info.plist"});
        plistPath.Should().NotBeNull();

        using var plistStream = iso.OpenFile(plistPath!, FileMode.Open);
        using var reader = new StreamReader(plistStream);
        var plistText = reader.ReadToEnd();
        plistText.Should().Contain("CFBundleIconFile");
    }

    [Fact]
    public async Task Volume_name_is_sanitized_reasonably()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "file.txt"), "ok");

        var outDmg = Path.Combine(tempRoot.Path, "Out.dmg");
        await DmgIsoBuilder.Create(publish, outDmg, "my app: with spaces & unicode — test……………………………………………");

        using var fs = File.OpenRead(outDmg);
        using var iso = new CDReader(fs, true);
        var bundle = "myappwithspacesunicodetest.app";
        FileExistsAny(iso, new[]{
            $"{bundle}/Contents/MacOS/file.txt",
            $"/{bundle}/Contents/MacOS/file.txt",
            $"\\{bundle}\\Contents\\MacOS\\file.txt"}).Should().BeTrue();
    }

    private static bool FileExistsAny(CDReader iso, IEnumerable<string> candidates)
        => candidates.Any(p => iso.FileExists(p));
    private static bool DirExistsAny(CDReader iso, IEnumerable<string> candidates)
        => candidates.Any(p => iso.DirectoryExists(p));
    private static string? FirstExistingPath(CDReader iso, IEnumerable<string> candidates)
        => candidates.FirstOrDefault(iso.FileExists);
}

file sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dmgtest-" + Guid.NewGuid());
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}

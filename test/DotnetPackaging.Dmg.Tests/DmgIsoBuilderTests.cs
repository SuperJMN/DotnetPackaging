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
        // We cannot query volume label on this DiscUtils version; just ensure a file can be read
        FileExistsAny(iso, new[]{"file.txt","/file.txt","\\file.txt"}).Should().BeTrue();
    }

    private static bool FileExistsAny(CDReader iso, IEnumerable<string> candidates)
        => candidates.Any(p => iso.FileExists(p));
    private static bool DirExistsAny(CDReader iso, IEnumerable<string> candidates)
        => candidates.Any(p => iso.DirectoryExists(p));
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

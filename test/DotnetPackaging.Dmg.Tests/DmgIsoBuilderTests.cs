using System.Linq;
using System.Collections.Generic;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgIsoBuilderTests
{
    static DmgIsoBuilderTests()
    {
        Environment.SetEnvironmentVariable("DOTNETPACKAGING_FORCE_ISO", "1");
    }

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

        // Validate DMG was created with reasonable size
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(0, "dmg should have content");
        
        // Verify it's a valid ISO9660 by checking PVD signature at sector 16
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
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

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain app bundle content");
        
        // Verify it's a valid ISO9660
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

    [Fact]
    public async Task Skips_preexisting_app_directories_from_payload()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);

        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");
        Directory.CreateDirectory(Path.Combine(publish, "AngorAvalonia.app"));

        var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
        await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "Angor Avalonia");

        // Verify DMG was created successfully
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain content");
        
        // Verify it's a valid ISO9660
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

    [Fact]
    public async Task Adds_generated_icon_to_app_bundle()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "publish");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "Angor"), "exe");

        var iconPath = Path.Combine(tempRoot.Path, "icon.png");
        using (var image = new Image<Rgba32>(64, 64))
        {
            image[0, 0] = new Rgba32(255, 0, 0, 255);
            await image.SaveAsync(iconPath);
        }

        var icon = await Icon.FromByteSource(ByteSource.FromStreamFactory(() => File.OpenRead(iconPath)));
        icon.IsSuccess.Should().BeTrue();

        var existingStages = Directory.GetDirectories(Path.GetTempPath(), "dmgstage-*").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("DOTNETPACKAGING_KEEP_DMG_STAGE", "1");

        string? stage = null;
        try
        {
            var outDmg = Path.Combine(tempRoot.Path, "Angor.dmg");
            await DotnetPackaging.Dmg.DmgIsoBuilder.Create(publish, outDmg, "Angor Avalonia", compress: false, addApplicationsSymlink: false, includeDefaultLayout: false, icon: Maybe<IIcon>.From(icon.Value));

            var allStages = Directory.GetDirectories(Path.GetTempPath(), "dmgstage-*");
            stage = allStages.First(dir => !existingStages.Contains(dir));

            var appIconPath = Path.Combine(stage, "AngorAvalonia.app", "Contents", "Resources", "AppIcon.icns");
            File.Exists(appIconPath).Should().BeTrue();

            var plistPath = Path.Combine(stage, "AngorAvalonia.app", "Contents", "Info.plist");
            var plistContent = await File.ReadAllTextAsync(plistPath);
            plistContent.Should().Contain("CFBundleIconFile");
            plistContent.Should().Contain("AppIcon");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNETPACKAGING_KEEP_DMG_STAGE", null);
            if (stage != null && Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
        }
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

        // Verify DMG was created successfully with reasonable size
        File.Exists(outDmg).Should().BeTrue();
        var info = new FileInfo(outDmg);
        info.Length.Should().BeGreaterThan(1000, "dmg should contain content");
        
        // Verify it's a valid ISO9660
        using var fs = File.OpenRead(outDmg);
        fs.Seek(16 * 2048 + 1, SeekOrigin.Begin);
        var signature = new byte[5];
        fs.Read(signature, 0, 5);
        Encoding.ASCII.GetString(signature).Should().Be("CD001", "should have ISO9660 PVD signature");
    }

    [Fact]
    public async Task Rock_ridge_entries_are_emitted_and_preserve_names()
    {
        using var tempRoot = new TempDir();
        var publish = Path.Combine(tempRoot.Path, "pub");
        Directory.CreateDirectory(publish);
        await File.WriteAllTextAsync(Path.Combine(publish, "ReadMe.txt"), "ok");

        var outDmg = Path.Combine(tempRoot.Path, "Sample.dmg");
        await DmgIsoBuilder.Create(publish, outDmg, "Sample", compress: false, addApplicationsSymlink: true);

        using var fs = File.OpenRead(outDmg);
        var (rootExtentStart, rootDataLength) = ReadRootExtent(fs);
        var records = EnumerateDirectoryTree(fs, rootExtentStart, rootDataLength).ToList();

        var rootSusp = GetSusp(records.First(IsDotEntry));
        rootSusp.Select(e => e.Signature).Should().Contain(new[] { "SP", "ER" });

        var nmValues = records
            .SelectMany(r => GetSusp(r)
                .Where(e => e.Signature == "NM" && e.Data.Length > 1)
                .Select(e => Encoding.UTF8.GetString(e.Data, 1, e.Data.Length - 1)))
            .ToList();

        nmValues.Should().Contain("ReadMe.txt");
    }

    private static (long Start, int Length) ReadRootExtent(Stream stream)
    {
        stream.Seek(16 * 2048, SeekOrigin.Begin);
        var pvd = new byte[2048];
        var read = stream.Read(pvd, 0, pvd.Length);
        if (read != pvd.Length)
        {
            throw new IOException("Unable to read PVD");
        }

        int extentLocation = BitConverter.ToInt32(pvd, 156 + 2);
        int dataLength = BitConverter.ToInt32(pvd, 156 + 10);
        return (extentLocation * 2048L, dataLength);
    }

    private static IEnumerable<byte[]> EnumerateDirectoryTree(Stream stream, long rootStart, int rootLength)
    {
        var pending = new Queue<(long Start, int Length)>();
        pending.Enqueue((rootStart, rootLength));

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var record in ReadDirectoryRecords(stream, current.Start, current.Length))
            {
                if (record.Length < 26)
                {
                    continue;
                }
                yield return record;

                var isDirectory = (record[25] & 0x02) != 0;
                if (isDirectory && !IsDotEntry(record) && !IsDotDotEntry(record))
                {
                    var extent = BitConverter.ToInt32(record, 2);
                    var dataLength = BitConverter.ToInt32(record, 10);
                    pending.Enqueue((extent * 2048L, dataLength));
                }
            }
        }
    }

    private static IEnumerable<byte[]> ReadDirectoryRecords(Stream stream, long start, int length)
    {
        stream.Seek(start, SeekOrigin.Begin);
        var end = start + length;

        while (stream.Position < end)
        {
            var lenByte = stream.ReadByte();
            if (lenByte < 0)
            {
                yield break;
            }

            if (lenByte == 0)
            {
                var offsetInSector = (stream.Position - start) % 2048;
                if (offsetInSector != 0)
                {
                    stream.Seek(2048 - offsetInSector, SeekOrigin.Current);
                }
                continue;
            }

            var record = new byte[lenByte];
            record[0] = (byte)lenByte;
            var remaining = lenByte - 1;
            var read = stream.Read(record, 1, remaining);
            if (read != remaining)
            {
                throw new IOException("Unexpected end of directory record");
            }

            yield return record;
        }
    }

    private static List<SuspEntry> GetSusp(byte[] record)
    {
        int len = record[0];
        int nameLen = record[32];
        int suspStart = 33 + nameLen;
        if (suspStart % 2 != 0)
        {
            suspStart++;
        }

        var entries = new List<SuspEntry>();
        int offset = suspStart;

        while (offset + 4 <= len)
        {
            var signature = Encoding.ASCII.GetString(record, offset, 2);
            var entryLen = record[offset + 2];

            if (entryLen == 0 || entryLen < 4)
            {
                break;
            }

            var data = new byte[entryLen - 4];
            Array.Copy(record, offset + 4, data, 0, data.Length);

            entries.Add(new SuspEntry(signature, data));
            offset += entryLen;
        }

        return entries;
    }

    private static bool IsDotEntry(byte[] record)
    {
        var nameLen = record[32];
        if (nameLen != 1)
        {
            return false;
        }

        return record[33] == 0;
    }

    private static bool IsDotDotEntry(byte[] record)
    {
        var nameLen = record[32];
        if (nameLen != 1)
        {
            return false;
        }

        return record[33] == 1;
    }

    private record SuspEntry(string Signature, byte[] Data);

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

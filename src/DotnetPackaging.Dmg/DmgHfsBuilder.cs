using System.Buffers.Binary;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Dmg.Hfs.Builder;
using DotnetPackaging.Dmg.Hfs.Files;
using DotnetPackaging.Formats.Dmg.Udif;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg;

/// <summary>
/// Cross-platform DMG builder using native HFS+ implementation.
/// Produces proper .dmg files that mount on macOS without any external tools.
/// </summary>
public static class DmgHfsBuilder
{
    private static readonly IReadOnlyDictionary<int, string> IcnsChunkTypes = new Dictionary<int, string>
    {
        [16] = "icp4",
        [32] = "icp5",
        [64] = "icp6",
        [128] = "ic07",
        [256] = "ic08",
        [512] = "ic09",
        [1024] = "ic10"
    };

    /// <summary>
    /// Creates a DMG from a source folder.
    /// </summary>
    public static async Task Create(
        string sourceFolder, 
        string outputPath, 
        string volumeName, 
        bool compress = false, 
        bool addApplicationsSymlink = false, 
        bool includeDefaultLayout = true, 
        Maybe<IIcon> icon = default,
        string? executableName = null)
    {
        var builder = HfsVolumeBuilder.Create(SanitizeVolumeName(volumeName));

        // Add Applications symlink if requested
        if (addApplicationsSymlink)
        {
            builder.AddSymlink("Applications", "/Applications");
        }

        // Check for .app bundles
        var appBundles = Directory.EnumerateDirectories(sourceFolder, "*.app", SearchOption.TopDirectoryOnly).ToList();

        if (appBundles.Any())
        {
            // Copy existing .app bundles
            foreach (var bundle in appBundles)
            {
                var bundleName = Path.GetFileName(bundle);
                if (bundleName == null) continue;
                
                var appDir = builder.AddDirectory(bundleName);
                await AddDirectoryContentsRecursive(appDir, bundle);
            }
        }
        else
        {
            // Create .app bundle from flat files
            var bundleName = SanitizeBundleName(volumeName) + ".app";
            var appDir = builder.AddDirectory(bundleName);
            var contentsDir = appDir.AddDirectory("Contents");
            var macOsDir = contentsDir.AddDirectory("MacOS");
            var resourcesDir = contentsDir.AddDirectory("Resources");

            // Add all files to MacOS folder
            await AddDirectoryContents(macOsDir, sourceFolder, 
                path => path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".icns", StringComparison.OrdinalIgnoreCase));

            // Handle icon
            var appIcon = await PrepareAppIcon(sourceFolder, resourcesDir, icon);

            // Generate Info.plist
            var exeName = executableName ?? GuessExecutableName(sourceFolder, volumeName);
            var plist = GenerateMinimalPlist(volumeName, exeName, appIcon.HasValue ? appIcon.Value : null);
            contentsDir.AddFile("Info.plist", Encoding.UTF8.GetBytes(plist));
            contentsDir.AddFile("PkgInfo", Encoding.ASCII.GetBytes("APPL????"));
        }

        // Add DMG adornments (background, .DS_Store)
        if (includeDefaultLayout)
        {
            var backgroundDir = builder.AddDirectory(".background");
            backgroundDir.AddFile("Background.png", DefaultDmgLayout.BackgroundPng.ToStream().ReadAllBytes());
            builder.AddFile(".DS_Store", DefaultDmgLayout.DsStore.ToStream().ReadAllBytes());
        }

        // Build the HFS+ volume
        var volume = builder.Build();
        var hfsBytes = HfsVolumeWriter.WriteToBytes(volume);
        
        // Wrap HFS+ volume in UDIF format (DMG)
        using var hfsStream = new MemoryStream(hfsBytes);
        using var dmgStream = new MemoryStream();
        
        var udifWriter = new UdifWriter
        {
            CompressionType = compress ? CompressionType.Zlib : CompressionType.Raw
        };
        
        udifWriter.Create(hfsStream, dmgStream);
        
        await File.WriteAllBytesAsync(outputPath, dmgStream.ToArray());
    }

    private static async Task AddDirectoryContentsRecursive(HfsDirectory target, string sourcePath)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            var subDir = target.AddDirectory(dirName);
            await AddDirectoryContentsRecursive(subDir, dir);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            var fileBytes = await File.ReadAllBytesAsync(file);
            target.AddFile(fileName, fileBytes);
        }
    }

    private static async Task AddDirectoryContents(HfsDirectory target, string sourcePath, Func<string, bool>? shouldSkip = null)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            if (shouldSkip?.Invoke(dir) == true) continue;
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            var subDir = target.AddDirectory(dirName);
            await AddDirectoryContentsRecursive(subDir, dir);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            if (shouldSkip?.Invoke(file) == true) continue;
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            var fileBytes = await File.ReadAllBytesAsync(file);
            target.AddFile(fileName, fileBytes);
        }
    }

    private static async Task<Maybe<string>> PrepareAppIcon(string sourceFolder, HfsDirectory resources, Maybe<IIcon> providedIcon)
    {
        if (providedIcon.HasValue)
        {
            return await CreateIcns(providedIcon.Value, resources);
        }

        var existingIcns = FindIcnsIcon(sourceFolder);
        if (existingIcns != null)
        {
            var fileName = Path.GetFileName(existingIcns)!;
            var iconBytes = await File.ReadAllBytesAsync(existingIcns);
            resources.AddFile(fileName, iconBytes);
            return Path.GetFileNameWithoutExtension(existingIcns);
        }

        var pngIcon = FindPngIcon(sourceFolder);
        if (pngIcon != null)
        {
            var iconResult = await Icon.FromByteSource(ByteSource.FromStreamFactory(() => File.OpenRead(pngIcon)));
            if (iconResult.IsSuccess)
            {
                return await CreateIcns(iconResult.Value, resources);
            }
        }

        return Maybe<string>.None;
    }

    private static async Task<Maybe<string>> CreateIcns(IIcon icon, HfsDirectory resources)
    {
        var iconBytes = await icon.Bytes.ToList();
        var pngBytes = iconBytes.SelectMany(bytes => bytes).ToArray();
        var chunkType = SelectChunkType(icon.Size);
        var icnsBytes = BuildIcns(pngBytes, chunkType);
        var iconFileName = "AppIcon.icns";
        resources.AddFile(iconFileName, icnsBytes);
        return Path.GetFileNameWithoutExtension(iconFileName);
    }

    private static byte[] BuildIcns(IReadOnlyList<byte> pngBytes, string chunkType)
    {
        var iconChunkLength = 8 + pngBytes.Count;
        var totalLength = 8 + iconChunkLength;

        var buffer = new byte[totalLength];
        Encoding.ASCII.GetBytes("icns").CopyTo(buffer, 0);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(4), totalLength);
        Encoding.ASCII.GetBytes(chunkType).CopyTo(buffer, 8);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(12), iconChunkLength);
        for (var i = 0; i < pngBytes.Count; i++)
        {
            buffer[16 + i] = pngBytes[i];
        }

        return buffer;
    }

    private static string SelectChunkType(int size)
    {
        if (IcnsChunkTypes.TryGetValue(size, out var chunkType))
        {
            return chunkType;
        }

        var closest = IcnsChunkTypes
            .OrderBy(entry => Math.Abs(entry.Key - size))
            .First();

        return closest.Value;
    }

    private static string? FindIcnsIcon(string sourceFolder)
    {
        var icons = Directory.EnumerateFiles(sourceFolder, "*.icns", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path)!.Equals(".VolumeIcon.icns", StringComparison.OrdinalIgnoreCase));

        return icons.FirstOrDefault();
    }

    private static string? FindPngIcon(string sourceFolder)
    {
        var preferredNames = new[]
        {
            "icon-512.png",
            "icon-256.png",
            "icon.png",
            "app.png"
        };

        foreach (var preferred in preferredNames)
        {
            var candidate = Path.Combine(sourceFolder, preferred);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory.EnumerateFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    private static string GuessExecutableName(string sourceFolder, string volumeName)
    {
        var candidates = Directory.EnumerateFiles(sourceFolder)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();
        var vn = new string(volumeName.Where(char.IsLetterOrDigit).ToArray());
        
        // 1. Exact match (e.g. EvaluacionesApp.Desktop == EvaluacionesApp.Desktop)
        var match = candidates.FirstOrDefault(n => string.Equals(n, volumeName, StringComparison.OrdinalIgnoreCase));
        
        // 2. Match without extension (e.g. My App.exe -> matches "My App" volume?) - Less likely on Mac but possible
        if (match == null)
        {
            match = candidates.FirstOrDefault(n => string.Equals(Path.GetFileNameWithoutExtension(n), volumeName, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Match against sanitized name (Existing logic)
        if (match == null)
        {
            match = candidates.FirstOrDefault(n => string.Equals(n, vn, StringComparison.OrdinalIgnoreCase));
        }

        // 4. Prefer files without extension (Unix binaries)
        if (match == null)
        {
            match = candidates.FirstOrDefault(n => Path.GetExtension(n) == string.Empty);
        }

        // 5. Fallback
        match ??= candidates.FirstOrDefault() ?? vn;
        
        return match;
    }

    private static string GenerateMinimalPlist(string displayName, string executable, string? iconName)
    {
        var identifier = $"com.{SanitizeBundleName(displayName).Trim('-').Trim('_').ToLowerInvariant()}";
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple Computer//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
  <dict>
    <key>CFBundleName</key>
    <string>{System.Security.SecurityElement.Escape(displayName)}</string>
    <key>CFBundleIdentifier</key>
    <string>{System.Security.SecurityElement.Escape(identifier)}</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>{System.Security.SecurityElement.Escape(executable)}</string>
{(iconName == null ? string.Empty : $"    <key>CFBundleIconFile</key>\n    <string>{System.Security.SecurityElement.Escape(iconName)}</string>\n")}  </dict>
</plist>
""";
    }

    private static string SanitizeVolumeName(string name)
    {
        var upper = new string(name.ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        if (upper.Length > 32) upper = upper[..32];
        if (string.IsNullOrWhiteSpace(upper)) upper = "APP";
        return upper;
    }

    private static string SanitizeBundleName(string name)
    {
        var cleaned = new string(name.Where(ch => char.IsLetterOrDigit(ch) || ch=='_' || ch=='-').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "App" : cleaned;
    }

}

/// <summary>
/// Stream extension methods.
/// </summary>
internal static class StreamExtensionsDmg
{
    public static byte[] ReadAllBytes(this Stream stream)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}

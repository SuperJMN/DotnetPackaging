using System.Buffers.Binary;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Dmg.Hfs.Builder;
using DotnetPackaging.Dmg.Hfs.Files;
using DotnetPackaging.Formats.Dmg.Udif;
using Zafiro.DivineBytes;
using Path = System.IO.Path;
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace DotnetPackaging.Dmg;

/// <summary>
/// Cross-platform DMG builder using native HFS+ implementation.
/// Produces proper .dmg files that mount on macOS without any external tools.
/// </summary>
internal static class DmgHfsBuilder
{
    private const string BackgroundDirectoryName = ".background";
    private const string DefaultBackgroundFileName = "Background.png";
    private const string DsStoreFileName = ".DS_Store";
    private const string VolumeIconFileName = ".VolumeIcon.icns";

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
        string? executableName = null,
        Maybe<IByteSource> infoPlist = default,
        Maybe<string> bundleIdentifier = default,
        Maybe<string> bundleVersion = default,
        Maybe<string> vendor = default)
    {
        var volume = await BuildVolume(
            sourceFolder,
            volumeName,
            addApplicationsSymlink,
            includeDefaultLayout,
            icon,
            executableName,
            infoPlist,
            bundleIdentifier,
            bundleVersion,
            vendor).ConfigureAwait(false);

        using var hfsFile = MaterializedByteSourceFile.Create(".hfs");
        await using (var hfsOutput = File.Open(hfsFile.Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            await HfsVolumeWriter.WriteToAsync(volume, hfsOutput).ConfigureAwait(false);
        }

        await using var hfsStream = File.Open(hfsFile.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var dmgStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var udifWriter = new UdifWriter
        {
            CompressionType = compress ? CompressionType.Zlib : CompressionType.Raw
        };

        udifWriter.Create(hfsStream, dmgStream);
    }

    internal static async Task<HfsVolume> BuildVolume(
        string sourceFolder,
        string volumeName,
        bool addApplicationsSymlink = false,
        bool includeDefaultLayout = true,
        Maybe<IIcon> icon = default,
        string? executableName = null,
        Maybe<IByteSource> infoPlist = default,
        Maybe<string> bundleIdentifier = default,
        Maybe<string> bundleVersion = default,
        Maybe<string> vendor = default)
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
                AddDirectoryContentsRecursive(appDir, bundle);
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
            var exeName = executableName ?? GuessExecutableName(sourceFolder, volumeName);

            // Add all files to MacOS folder
            AddDirectoryContents(
                macOsDir,
                sourceFolder,
                path => path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".icns", StringComparison.OrdinalIgnoreCase) ||
                        IsRootInfoPlist(sourceFolder, path) ||
                        IsDmgAdornment(sourceFolder, path),
                path => IsExecutablePath(sourceFolder, path, exeName) ? HfsFileModes.Regular0755 : HfsFileModes.Regular0644,
                _ => HfsFileModes.Regular0644);

            // Handle icon
            var appIcon = await PrepareAppIcon(sourceFolder, resourcesDir, icon);

            await AddInfoPlist(contentsDir, sourceFolder, infoPlist, volumeName, exeName, appIcon, bundleIdentifier, bundleVersion, vendor);
            contentsDir.AddFile("PkgInfo", Encoding.ASCII.GetBytes("APPL????"));
        }

        AddDmgAdornments(builder, sourceFolder, includeDefaultLayout);

        return builder.Build();
    }

    private static void AddDirectoryContentsRecursive(HfsDirectory target, string sourcePath)
    {
        AddDirectoryContentsRecursive(target, sourcePath, GetFileMode);
    }

    private static void AddDirectoryContentsRecursive(HfsDirectory target, string sourcePath, Func<string, ushort> getFileMode)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            var subDir = target.AddDirectory(dirName);
            AddDirectoryContentsRecursive(subDir, dir, getFileMode);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            AddFileFromPath(target, fileName, file, getFileMode(file));
        }
    }

    private static void AddDirectoryContents(
        HfsDirectory target,
        string sourcePath,
        Func<string, bool>? shouldSkip = null,
        Func<string, ushort>? getFileMode = null,
        Func<string, ushort>? getRecursiveFileMode = null)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            if (shouldSkip?.Invoke(dir) == true) continue;
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            var subDir = target.AddDirectory(dirName);
            AddDirectoryContentsRecursive(subDir, dir, getRecursiveFileMode ?? getFileMode ?? GetFileMode);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            if (shouldSkip?.Invoke(file) == true) continue;
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            AddFileFromPath(target, fileName, file, getFileMode?.Invoke(file) ?? GetFileMode(file));
        }
    }

    private static async Task AddInfoPlist(
        HfsDirectory contentsDir,
        string sourceFolder,
        Maybe<IByteSource> providedInfoPlist,
        string volumeName,
        string executableName,
        Maybe<string> appIcon,
        Maybe<string> bundleIdentifier,
        Maybe<string> bundleVersion,
        Maybe<string> vendor)
    {
        var sourceInfoPlistPath = FindSourceInfoPlist(sourceFolder);
        var sourceInfoPlist = sourceInfoPlistPath.HasValue
            ? Maybe<IByteSource>.From(FileByteSource.OpenRead(sourceInfoPlistPath.Value))
            : Maybe<IByteSource>.None;
        var customInfoPlist = providedInfoPlist.Or(sourceInfoPlist);

        if (customInfoPlist.HasValue)
        {
            await AddFile(contentsDir, "Info.plist", customInfoPlist.Value);
            return;
        }

        var plist = GenerateMinimalPlist(
            volumeName,
            executableName,
            appIcon.HasValue ? appIcon.Value : null,
            bundleIdentifier,
            bundleVersion,
            vendor);
        contentsDir.AddFile("Info.plist", Encoding.UTF8.GetBytes(plist));
    }

    private static async Task AddFile(HfsDirectory target, string name, IByteSource source)
    {
        if (source.Length.HasValue)
        {
            target.AddFile(name, source, source.Length.Value);
            return;
        }

        var chunks = await source.Bytes.ToList();
        target.AddFile(name, chunks.SelectMany(bytes => bytes).ToArray());
    }

    private static Maybe<string> FindSourceInfoPlist(string sourceFolder)
    {
        var path = Path.Combine(sourceFolder, "Info.plist");
        return File.Exists(path) ? Maybe.From(path) : Maybe<string>.None;
    }

    private static bool IsRootInfoPlist(string sourceFolder, string path)
    {
        return IsRootEntry(sourceFolder, path, "Info.plist");
    }

    private static void AddDmgAdornments(HfsVolumeBuilder builder, string sourceFolder, bool includeDefaultLayout)
    {
        var backgroundPath = Path.Combine(sourceFolder, BackgroundDirectoryName);
        var hasCustomBackground = Directory.Exists(backgroundPath);
        if (hasCustomBackground || includeDefaultLayout)
        {
            var backgroundDir = builder.AddDirectory(BackgroundDirectoryName);
            if (hasCustomBackground)
            {
                AddDirectoryContentsRecursive(backgroundDir, backgroundPath);
            }

            var defaultBackgroundPath = Path.Combine(backgroundPath, DefaultBackgroundFileName);
            if (includeDefaultLayout && !File.Exists(defaultBackgroundPath))
            {
                backgroundDir.AddFile(DefaultBackgroundFileName, DefaultDmgLayout.BackgroundPng.ToStream().ReadAllBytes());
            }
        }

        var dsStorePath = Path.Combine(sourceFolder, DsStoreFileName);
        if (File.Exists(dsStorePath))
        {
            AddFileFromPath(builder.Root, DsStoreFileName, dsStorePath);
        }
        else if (includeDefaultLayout)
        {
            builder.AddFile(DsStoreFileName, DefaultDmgLayout.DsStore.ToStream().ReadAllBytes());
        }

        var volumeIconPath = Path.Combine(sourceFolder, VolumeIconFileName);
        if (File.Exists(volumeIconPath))
        {
            AddFileFromPath(builder.Root, VolumeIconFileName, volumeIconPath);
        }
    }

    private static bool IsDmgAdornment(string sourceFolder, string path)
    {
        return IsRootEntry(sourceFolder, path, BackgroundDirectoryName)
               || IsRootEntry(sourceFolder, path, DsStoreFileName)
               || IsRootEntry(sourceFolder, path, VolumeIconFileName);
    }

    private static bool IsRootEntry(string sourceFolder, string path, string name)
    {
        return string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase)
               && string.Equals(Path.GetDirectoryName(path), sourceFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExecutablePath(string sourceFolder, string path, string executableName)
    {
        return string.Equals(Path.GetFileName(path), executableName, StringComparison.Ordinal)
               && string.Equals(Path.GetDirectoryName(path), sourceFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static ushort GetFileMode(string path)
    {
#if NET6_0_OR_GREATER
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var mode = File.GetUnixFileMode(path);
            var permissions = (ushort)((uint)mode & 0x01FF);
            return HfsFileModes.Regular(permissions);
        }
#endif

        return HfsFileModes.Regular0644;
    }

    private static void AddFileFromPath(HfsDirectory target, string fileName, string path, ushort? fileMode = null)
    {
        var fileInfo = new FileInfo(path);
        target.AddFile(fileName, FileByteSource.OpenRead(fileInfo), fileInfo.Length, fileMode ?? GetFileMode(path));
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
            AddFileFromPath(resources, fileName, existingIcns);
            return Path.GetFileNameWithoutExtension(existingIcns);
        }

        var pngIcon = FindPngIcon(sourceFolder);
        if (pngIcon != null)
        {
            var iconResult = await Icon.FromByteSource(FileByteSource.OpenRead(pngIcon));
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

    private static string GenerateMinimalPlist(
        string displayName,
        string executable,
        string? iconName,
        Maybe<string> bundleIdentifier = default,
        Maybe<string> bundleVersion = default,
        Maybe<string> vendor = default)
    {
        var fallbackIdentifier = $"com.{SanitizeBundleName(displayName).Trim('-').Trim('_').ToLowerInvariant()}";
        var identifier = bundleIdentifier.GetValueOrDefault(fallbackIdentifier);
        var version = bundleVersion.GetValueOrDefault("1.0");
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
    <string>{System.Security.SecurityElement.Escape(version)}</string>
    <key>CFBundleShortVersionString</key>
    <string>{System.Security.SecurityElement.Escape(version)}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>{System.Security.SecurityElement.Escape(executable)}</string>
{(vendor.HasValue ? $"    <key>CFBundleGetInfoString</key>\n    <string>{System.Security.SecurityElement.Escape(vendor.Value)}</string>\n" : string.Empty)}
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

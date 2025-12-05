using System.Buffers.Binary;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Formats.Dmg.Iso;
using DotnetPackaging.Formats.Dmg.Udif;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg;

/// <summary>
/// Cross-platform DMG builder using UDIF format with Rock Ridge support.
/// Produces proper .dmg files that mount on macOS with full metadata preservation.
/// </summary>
public static class DmgIsoBuilder
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

    public static async Task Create(string sourceFolder, string outputPath, string volumeName, bool compress = false, bool addApplicationsSymlink = false, bool includeDefaultLayout = true, Maybe<IIcon> icon = default)
    {
        var stagingRoot = await StageContent(sourceFolder, volumeName, includeDefaultLayout, addApplicationsSymlink, icon);
        var keepStage = Environment.GetEnvironmentVariable("DOTNETPACKAGING_KEEP_DMG_STAGE") == "1";

        try
        {
            var forceIso = Environment.GetEnvironmentVariable("DOTNETPACKAGING_FORCE_ISO") == "1";

            if (OperatingSystem.IsMacOS() && !forceIso)
            {
                BuildWithHdiUtil(stagingRoot, outputPath, volumeName, compress);
            }
            else
            {
                BuildIso(stagingRoot, outputPath, volumeName, compress, addApplicationsSymlink);
            }
        }
        finally
        {
            if (!keepStage)
            {
                TryDeleteDirectory(stagingRoot);
            }
        }

        return;
    }

    private static void BuildIso(string stagingRoot, string outputPath, string volumeName, bool compress, bool addApplicationsSymlink)
    {
        var builder = new IsoBuilder(SanitizeVolumeName(volumeName));

        if (addApplicationsSymlink)
        {
            builder.Root.AddChild(new IsoSymlink("Applications", "/Applications"));
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(stagingRoot))
        {
            var name = Path.GetFileName(entry);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // Preserve Applications symlink without following it
                builder.Root.AddChild(new IsoSymlink(name, "/Applications"));
                continue;
            }

            if (Directory.Exists(entry))
            {
                var dir = builder.Root.AddDirectory(name);
                AddDirectoryRecursive(dir, entry);
            }
            else
            {
                builder.Root.AddChild(new IsoFile(name)
                {
                    ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(entry)),
                    SourcePath = entry
                });
            }
        }

        if (!compress)
        {
            using var dmgStream = File.Create(outputPath);
            builder.Build(dmgStream);
        }
        else
        {
            using var isoStream = new MemoryStream();
            builder.Build(isoStream);
            isoStream.Position = 0;

            using var dmgStream = File.Create(outputPath);
            var writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
            writer.Create(isoStream, dmgStream);
        }
    }

    private static void BuildWithHdiUtil(string stagingRoot, string outputPath, string volumeName, bool compress)
    {
        var format = compress ? "UDBZ" : "UDRO";

        var psi = new ProcessStartInfo
        {
            FileName = "hdiutil",
            ArgumentList =
            {
                "create",
                "-ov",
                "-fs", "HFS+",
                "-format", format,
                "-srcfolder", stagingRoot,
                "-volname", volumeName,
                outputPath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start hdiutil");
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"hdiutil failed ({proc.ExitCode}): {err}");
        }
    }

    private static async Task<string> StageContent(string sourceFolder, string volumeName, bool includeDefaultLayout, bool addApplicationsSymlink, Maybe<IIcon> icon)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "dmgstage-" + Guid.NewGuid());
        Directory.CreateDirectory(stagingRoot);

        var appBundles = Directory.EnumerateDirectories(sourceFolder, "*.app", SearchOption.TopDirectoryOnly).ToList();

        if (addApplicationsSymlink)
        {
            var linkPath = Path.Combine(stagingRoot, "Applications");
            if (!File.Exists(linkPath))
            {
                CreateSymlink(linkPath, "/Applications");
            }
        }

        if (appBundles.Any())
        {
            foreach (var bundle in appBundles)
            {
                var bundleName = Path.GetFileName(bundle);
                if (bundleName == null) continue;
                var dest = Path.Combine(stagingRoot, bundleName);
                CopyDirectory(bundle, dest);
            }

            AddDmgAdornments(stagingRoot, includeDefaultLayout);
        }
        else
        {
            var bundleName = SanitizeBundleName(volumeName) + ".app";
            var bundleRoot = Path.Combine(stagingRoot, bundleName);
            var contents = Path.Combine(bundleRoot, "Contents");
            var macOs = Path.Combine(contents, "MacOS");
            var resources = Path.Combine(contents, "Resources");
            Directory.CreateDirectory(macOs);
            Directory.CreateDirectory(resources);

            AddDirectoryContents(macOs, sourceFolder, shouldSkip: path => path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".icns", StringComparison.OrdinalIgnoreCase));

            var appIcon = await PrepareAppIcon(sourceFolder, resources, icon);

            AddDmgAdornments(stagingRoot, includeDefaultLayout);

            var exeName = GuessExecutableName(sourceFolder, volumeName);
            var plist = GenerateMinimalPlist(volumeName, exeName, appIcon.Match(
                value => value,
                () => null));
            File.WriteAllText(Path.Combine(contents, "Info.plist"), plist, Encoding.UTF8);
            File.WriteAllText(Path.Combine(contents, "PkgInfo"), "APPL????", Encoding.ASCII);
        }

        return stagingRoot;
    }

    private static void AddDirectoryRecursive(IsoDirectory target, string sourcePath)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            var subDir = target.AddDirectory(dirName);
            AddDirectoryRecursive(subDir, dir);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            target.AddChild(new IsoFile(fileName)
            {
                ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file)),
                SourcePath = file
            });
        }
    }

    private static void CreateSymlink(string linkPath, string target)
    {
        try
        {
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo("ln", new[] { "-s", target, linkPath })
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit();
            }
            else
            {
                // Fallback: create a directory as placeholder if symlink unsupported
                if (!Directory.Exists(linkPath))
                {
                    Directory.CreateDirectory(linkPath);
                }
            }
        }
        catch
        {
            // Best-effort; ignore failures
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static void AddDirectoryContents(string targetPath, string sourcePath, Func<string, bool>? shouldSkip = null)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            if (shouldSkip?.Invoke(dir) == true) continue;
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            var dest = Path.Combine(targetPath, dirName);
            CopyDirectory(dir, dest, shouldSkip);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            if (shouldSkip?.Invoke(file) == true) continue;
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            File.Copy(file, Path.Combine(targetPath, fileName), overwrite: true);
        }
    }

    private static void CopyDirectory(string source, string destination, Func<string, bool>? shouldSkip = null)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            if (shouldSkip?.Invoke(dir) == true) continue;
            var destSub = Path.Combine(destination, Path.GetFileName(dir)!);
            CopyDirectory(dir, destSub, shouldSkip);
        }

        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (shouldSkip?.Invoke(file) == true) continue;
            var destFile = Path.Combine(destination, Path.GetFileName(file)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static void AddDirectoryContents(IsoDirectory target, string sourcePath, Func<string, bool>? shouldSkip = null)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourcePath))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName == null) continue;

            if (shouldSkip?.Invoke(dir) == true) continue;

            var subDir = target.AddDirectory(dirName);
            AddDirectoryRecursive(subDir, dir);
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var fileName = Path.GetFileName(file);
            if (fileName == null) continue;

            if (shouldSkip?.Invoke(file) == true) continue;

            target.AddChild(new IsoFile(fileName)
            {
                ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file)),
                SourcePath = file
            });
        }
    }

    private static void AddDmgAdornments(string stagingRoot, bool includeDefaultLayout)
    {
        var volIconSource = Path.Combine(stagingRoot, ".VolumeIcon.icns");
        if (File.Exists(volIconSource))
        {
            // leave as-is
        }

        var backgroundDir = Path.Combine(stagingRoot, ".background");
        var hasBackgroundDir = Directory.Exists(backgroundDir);
        if (!hasBackgroundDir && includeDefaultLayout)
        {
            Directory.CreateDirectory(backgroundDir);
            var defaultBg = Path.Combine(backgroundDir, "Background.png");
            DefaultDmgLayout.BackgroundPng.WriteTo(defaultBg);
        }

        var dsStorePath = Path.Combine(stagingRoot, ".DS_Store");
        if (includeDefaultLayout && !File.Exists(dsStorePath))
        {
            DefaultDmgLayout.DsStore.WriteTo(dsStorePath);
        }
    }

    private static string GuessExecutableName(string sourceFolder, string volumeName)
    {
        var candidates = Directory.EnumerateFiles(sourceFolder)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();
        var vn = new string(volumeName.Where(char.IsLetterOrDigit).ToArray());
        var match = candidates.FirstOrDefault(n => string.Equals(n, vn, StringComparison.OrdinalIgnoreCase))
                   ?? candidates.FirstOrDefault(n => Path.GetExtension(n) == string.Empty)
                   ?? candidates.FirstOrDefault()
                   ?? vn;
        return match;
    }

    private static string GenerateMinimalPlist(string displayName, string executable, string? iconName)
    {
        var identifier = $"com.{SanitizeBundleName(displayName).Trim('-').Trim('_').ToLowerInvariant()}";
        return $"""
<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<!DOCTYPE plist PUBLIC \"-//Apple Computer//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
<plist version=\"1.0\">
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
{(iconName == null ? string.Empty : $"    <key>CFBundleIconFile</key>\n    <string>{System.Security.SecurityElement.Escape(iconName)}</string>\n")}
  </dict>
</plist>
""";
    }

    private static string SanitizeVolumeName(string name)
    {
        // ISO9660 volume id: uppercase A-Z, 0-9, underscore; max 32 chars. Joliet relaxes but keep simple.
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

    private static async Task<Maybe<string>> PrepareAppIcon(string sourceFolder, string resources, Maybe<IIcon> providedIcon)
    {
        if (providedIcon.HasValue)
        {
            return await CreateIcns(providedIcon.Value, resources);
        }

        var existingIcns = FindIcnsIcon(sourceFolder);
        if (existingIcns != null)
        {
            var fileName = Path.GetFileName(existingIcns)!;
            File.Copy(existingIcns, Path.Combine(resources, fileName), overwrite: true);
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

    private static async Task<Maybe<string>> CreateIcns(IIcon icon, string resources)
    {
        var iconBytes = await icon.Bytes.ToList();
        var pngBytes = iconBytes.SelectMany(bytes => bytes).ToArray();
        var chunkType = SelectChunkType(icon.Size);
        var icnsBytes = BuildIcns(pngBytes, chunkType);
        var iconFileName = "AppIcon.icns";
        var destination = Path.Combine(resources, iconFileName);
        Directory.CreateDirectory(resources);
        await File.WriteAllBytesAsync(destination, icnsBytes);
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
}

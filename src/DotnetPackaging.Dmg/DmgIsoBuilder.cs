using Zafiro.DivineBytes;
using System.Text;
using DotnetPackaging.Formats.Dmg.Iso;
using DotnetPackaging.Formats.Dmg.Udif;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg;

/// <summary>
/// Cross-platform DMG builder using UDIF format with Rock Ridge support.
/// Produces proper .dmg files that mount on macOS with full metadata preservation.
/// </summary>
public static class DmgIsoBuilder
{
    public static Task Create(string sourceFolder, string outputPath, string volumeName, bool compress = false, bool addApplicationsSymlink = false)
    {
        var builder = new IsoBuilder(SanitizeVolumeName(volumeName));

        if (addApplicationsSymlink)
        {
            builder.Root.AddChild(new IsoSymlink("Applications", "/Applications"));
        }

        var appBundles = Directory.EnumerateDirectories(sourceFolder, "*.app", SearchOption.TopDirectoryOnly).ToList();
        
        if (appBundles.Any())
        {
            // Copy pre-built .app bundles to image root
            foreach (var bundle in appBundles)
            {
                var bundleName = Path.GetFileName(bundle);
                if (bundleName == null) continue;
                
                var bundleDir = builder.Root.AddDirectory(bundleName);
                AddDirectoryRecursive(bundleDir, bundle);
            }

            AddDmgAdornments(builder.Root, sourceFolder);
        }
        else
        {
            // Create .app bundle structure from publish output
            var bundleName = SanitizeBundleName(volumeName) + ".app";
            var appBundle = builder.Root.AddDirectory(bundleName);
            var contents = appBundle.AddDirectory("Contents");
            var macOs = contents.AddDirectory("MacOS");
            var resources = contents.AddDirectory("Resources");

            // Copy application payload under Contents/MacOS
            AddDirectoryContents(
                macOs,
                sourceFolder,
                shouldSkip: path => path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ||
                                   path.EndsWith(".icns", StringComparison.OrdinalIgnoreCase));

            // Copy .icns files to Resources
            var appIcon = FindIcnsIcon(sourceFolder);
            if (appIcon != null)
            {
                var iconName = Path.GetFileName(appIcon);
                resources.AddChild(new IsoFile(iconName)
                {
                    ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(appIcon)),
                    SourcePath = appIcon
                });
            }

            AddDmgAdornments(builder.Root, sourceFolder);

            // Generate Info.plist
            var exeName = GuessExecutableName(sourceFolder, volumeName);
            var plist = GenerateMinimalPlist(volumeName, exeName, appIcon == null ? null : Path.GetFileNameWithoutExtension(appIcon));
            contents.AddChild(new IsoFile("Info.plist")
            {
                ContentSource = () => ByteSource.FromBytes(Encoding.UTF8.GetBytes(plist))
            });

            // Add PkgInfo
            contents.AddChild(new IsoFile("PkgInfo")
            {
                ContentSource = () => ByteSource.FromBytes(Encoding.ASCII.GetBytes("APPL????"))
            });
        }

        // Build ISO
        if (!compress)
        {
            // For uncompressed, output raw ISO for backward compatibility with tests
            using var dmgStream = File.Create(outputPath);
            builder.Build(dmgStream);
        }
        else
        {
            // For compressed, wrap in UDIF with Bzip2
            using var isoStream = new MemoryStream();
            builder.Build(isoStream);
            isoStream.Position = 0;

            using var dmgStream = File.Create(outputPath);
            var writer = new UdifWriter { CompressionType = CompressionType.Bzip2 };
            writer.Create(isoStream, dmgStream);
        }

        return Task.CompletedTask;
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

    private static void AddDmgAdornments(IsoDirectory root, string sourceFolder)
    {
        // Add .VolumeIcon.icns if present
        var volIcon = Path.Combine(sourceFolder, ".VolumeIcon.icns");
        if (File.Exists(volIcon))
        {
            root.AddChild(new IsoFile(".VolumeIcon.icns")
            {
                ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(volIcon)),
                SourcePath = volIcon
            });
        }

        // Add .background directory if present
        var backgroundDir = Path.Combine(sourceFolder, ".background");
        if (Directory.Exists(backgroundDir))
        {
            var bgDir = root.AddDirectory(".background");
            AddDirectoryRecursive(bgDir, backgroundDir);
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

    private static string? FindIcnsIcon(string sourceFolder)
    {
        var icons = Directory.EnumerateFiles(sourceFolder, "*.icns", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path)!.Equals(".VolumeIcon.icns", StringComparison.OrdinalIgnoreCase));

        return icons.FirstOrDefault();
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
}

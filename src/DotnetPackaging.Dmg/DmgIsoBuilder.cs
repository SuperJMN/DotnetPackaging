using System.Text;
using System.Linq;
using DiscUtils.Iso9660;

namespace DotnetPackaging.Dmg;

/// <summary>
/// Minimal cross-platform DMG builder using an ISO/UDF (UDTO) payload.
/// This produces a .dmg that mounts fine on macOS for simple drag & drop installs.
/// Follow-ups can add true UDIF/UDZO wrapping while keeping the same public API.
/// </summary>
public static class DmgIsoBuilder
{
    public static Task Create(string sourceFolder, string outputPath, string volumeName)
    {
        // Build a Joliet-enabled ISO so long filenames/casing are preserved reasonably.
        using var fs = File.Create(outputPath);
        var builder = new CDBuilder
        {
            UseJoliet = true,
            VolumeIdentifier = SanitizeVolumeName(volumeName),
        };

        var hasApp = Directory.EnumerateDirectories(sourceFolder, "*.app", SearchOption.TopDirectoryOnly).Any();
        if (hasApp)
        {
            // If a pre-built .app exists, copy the directory tree as-is to the image root
            AddDirectoryRecursive(builder, sourceFolder, ".", prefix: null);
        }
        else
        {
            var bundle = SanitizeBundleName(volumeName) + ".app";
            builder.AddDirectory(bundle);
            builder.AddDirectory($"{bundle}/Contents");
            builder.AddDirectory($"{bundle}/Contents/MacOS");
            builder.AddDirectory($"{bundle}/Contents/Resources");

            // Copy application payload under Contents/MacOS
            AddDirectoryRecursive(builder, sourceFolder, ".", prefix: $"{bundle}/Contents/MacOS");

            var appIcon = FindIcnsIcon(sourceFolder);
            if (appIcon != null)
            {
                var iconName = Path.GetFileName(appIcon);
                var iconBytes = File.ReadAllBytes(appIcon);
                builder.AddFile($"{bundle}/Contents/Resources/{iconName}", new MemoryStream(iconBytes, writable: false));
            }

            // Hoist DMG adornments (if present) at image root for macOS Finder niceties
            var volIcon = Path.Combine(sourceFolder, ".VolumeIcon.icns");
            if (File.Exists(volIcon))
            {
                var bytes = File.ReadAllBytes(volIcon);
                builder.AddFile(".VolumeIcon.icns", new MemoryStream(bytes, writable: false));
            }
            var backgroundDir = Path.Combine(sourceFolder, ".background");
            if (Directory.Exists(backgroundDir))
            {
                AddDirectoryRecursive(builder, sourceFolder, ".background", prefix: null);
            }

            // Add a minimal Info.plist
            var exeName = GuessExecutableName(sourceFolder, volumeName);
            var plist = GenerateMinimalPlist(volumeName, exeName, appIcon == null ? null : Path.GetFileNameWithoutExtension(appIcon));
            builder.AddFile($"{bundle}/Contents/Info.plist", new MemoryStream(Encoding.UTF8.GetBytes(plist), writable: false));
        }

        builder.Build(fs);
        return Task.CompletedTask;
    }

    private static void AddDirectoryRecursive(CDBuilder builder, string root, string rel, string? prefix)
    {
        var abs = Path.Combine(root, rel);
        var targetDir = prefix == null || rel == "." ? rel : Path.Combine(prefix, rel);
        if (rel != ".")
        {
            builder.AddDirectory(targetDir.Replace('\\','/'));
        }

        foreach (var dir in Directory.EnumerateDirectories(abs))
        {
            var name = Path.GetFileName(dir);
            if (name == null) continue;
            var nextRel = rel == "." ? name : Path.Combine(rel, name);
            AddDirectoryRecursive(builder, root, nextRel, prefix);
        }

        foreach (var file in Directory.EnumerateFiles(abs))
        {
            var name = Path.GetFileName(file);
            if (name == null) continue;
            var relPath = rel == "." ? name : Path.Combine(rel, name);
            var finalPath = prefix == null ? relPath : Path.Combine(prefix, relPath);
            var bytes = File.ReadAllBytes(file);
            builder.AddFile(finalPath.Replace('\\','/'), new MemoryStream(bytes, writable: false));
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

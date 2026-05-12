using CSharpFunctionalExtensions;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Path = System.IO.Path;

namespace DotnetPackaging.Exe;

internal static class SetupLogoDiscovery
{
    private static readonly string[] PreferredRelativePaths =
    [
        "logo.png",
        "icon.png",
        "assets/logo.png",
        "Assets/logo.png",
        "resources/logo.png",
        "Resources/logo.png",
        "images/logo.png",
        "Images/logo.png",
        "icons/logo.png",
        "Icons/logo.png",
        "assets/icon.png",
        "Assets/icon.png",
        "resources/icon.png",
        "Resources/icon.png",
        "images/icon.png",
        "Images/icon.png",
        "icons/icon.png",
        "Icons/icon.png"
    ];

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        "bin",
        "obj",
        "artifacts",
        "packages",
        "node_modules"
    };

    public static Maybe<IByteSource> Discover(IContainer publishDirectory, Maybe<FileInfo> projectFile, ILogger logger)
    {
        return DiscoverInPublishedOutput(publishDirectory, logger)
            .Or(() => DiscoverNearProject(projectFile, logger));
    }

    private static Maybe<IByteSource> DiscoverInPublishedOutput(IContainer publishDirectory, ILogger logger)
    {
        var candidate = publishDirectory
            .ResourcesWithPathsRecursive()
            .Select(resource => new
            {
                Resource = (IByteSource)resource,
                RelativePath = Normalize(resource.FullPath().ToString())
            })
            .Select(candidate => new
            {
                candidate.Resource,
                candidate.RelativePath,
                Score = Score(candidate.RelativePath)
            })
            .Where(candidate => candidate.Score < int.MaxValue)
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.RelativePath.Length)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (candidate is null)
        {
            return Maybe<IByteSource>.None;
        }

        logger.Information("Auto-detected setup logo in publish output: {LogoPath}", candidate.RelativePath);
        return Maybe<IByteSource>.From(candidate.Resource);
    }

    private static Maybe<IByteSource> DiscoverNearProject(Maybe<FileInfo> projectFile, ILogger logger)
    {
        if (projectFile.HasNoValue || projectFile.Value.Directory is null)
        {
            return Maybe<IByteSource>.None;
        }

        var searchRoot = FindSearchRoot(projectFile.Value.Directory);
        var candidate = FindPreferredLogo(projectFile.Value.Directory, searchRoot)
                        ?? FindRecursiveLogo(searchRoot);

        if (candidate is null)
        {
            return Maybe<IByteSource>.None;
        }

        logger.Information("Auto-detected setup logo near project: {LogoPath}", candidate);
        return Maybe<IByteSource>.From(FileByteSource.OpenRead(candidate));
    }

    private static string? FindPreferredLogo(DirectoryInfo projectDirectory, DirectoryInfo searchRoot)
    {
        var current = projectDirectory;
        while (current is not null)
        {
            foreach (var relativePath in PreferredRelativePaths)
            {
                var candidate = Path.Combine(current.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            if (SameDirectory(current, searchRoot))
            {
                break;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? FindRecursiveLogo(DirectoryInfo searchRoot)
    {
        return EnumerateLogoCandidates(searchRoot.FullName)
            .Select(path => new
            {
                Path = path,
                RelativePath = Normalize(Path.GetRelativePath(searchRoot.FullName, path)),
                Score = Score(Normalize(Path.GetRelativePath(searchRoot.FullName, path)))
            })
            .Where(candidate => candidate.Score < int.MaxValue)
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.RelativePath.Length)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateLogoCandidates(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (IsLogoCandidateName(name))
                {
                    yield return file;
                }
            }

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                var name = Path.GetFileName(subdirectory);
                if (!SkippedDirectories.Contains(name))
                {
                    pending.Push(subdirectory);
                }
            }
        }
    }

    private static DirectoryInfo FindSearchRoot(DirectoryInfo projectDirectory)
    {
        var current = projectDirectory;
        while (current.Parent is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current;
            }

            current = current.Parent;
        }

        return projectDirectory;
    }

    private static int Score(string relativePath)
    {
        var normalized = Normalize(relativePath);
        var fileName = Path.GetFileName(normalized);
        var depth = normalized.Count(c => c == '/');

        if (fileName.Equals("logo.png", StringComparison.OrdinalIgnoreCase)) return depth;
        if (fileName.Equals("icon.png", StringComparison.OrdinalIgnoreCase)) return 20 + depth;
        if (fileName.Contains("logo", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return 60 + depth;
        if (fileName.Contains("icon", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return 80 + depth;

        return int.MaxValue;
    }

    private static bool IsLogoCandidateName(string fileName)
    {
        return fileName.Equals("logo.png", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("icon.png", StringComparison.OrdinalIgnoreCase)
               || (fileName.Contains("logo", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
               || (fileName.Contains("icon", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static bool SameDirectory(DirectoryInfo left, DirectoryInfo right)
    {
        return string.Equals(left.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}

using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class FileHelper
{
    public static Task<Result<IEnumerable<RootedFile>>> GetExecutables(IDirectory buildDirectory)
    {
        return buildDirectory.GetFilesInTree(ZafiroPath.Empty)
            .Bind(ToExecutableEntries)
            .Map(tuples => tuples.Where(x => x.IsExec))
            .Map(tuples => tuples.Select(tuple => tuple.Item2));
    }

    private static Task<Result<IEnumerable<(bool IsExec, RootedFile IMyFile)>>> ToExecutableEntries(IEnumerable<IRootedFile> files)
    {
        return files
            .Select(x => x.IsExecutable()
                .Map(isExec => (IsExec: isExec, new RootedFile(x.Path, x.Rooted))))
            .Combine();
    }
}
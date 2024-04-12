using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

public class FileHelper
{
    public static Task<Result<IEnumerable<(ZafiroPath Path, IFile Blob)>>> GetExecutables(IDirectory buildDirectory)
    {
        return buildDirectory.GetFilesInTree(ZafiroPath.Empty)
            .Bind(ToExecutableEntries)
            .Map(tuples => tuples.Where(x => x.IsExec))
            .Map(tuples => tuples.Select(tuple => (tuple.Path, tuple.Blob)));
    }

    private static Task<Result<IEnumerable<(bool IsExec, ZafiroPath Path, IFile Blob)>>> ToExecutableEntries(IEnumerable<(ZafiroPath Path, IFile Blob)> files)
    {
        return files.Select(x => x.IsExecutable().Map(isExec => (IsExec: isExec, x.Path, x.Blob))).Combine();
    }
}
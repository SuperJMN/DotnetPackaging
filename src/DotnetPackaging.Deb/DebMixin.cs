using CSharpFunctionalExtensions;
//using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.FileSystem;

namespace DotnetPackaging.Deb;

public static class DebMixin
{
    //public static Task<Result<Stream>> ToStream(this TarFile tarfile)
    //{
    //    var ms = new MemoryStream();
    //    return TarWriter.Write(tarfile, ms)
    //        .Map(() => (Stream)ms)
    //        .Tap(() => ms.Position = 0);
    //}

    //public static IEnumerable<ZafiroPath> DirectoryPaths(this IEnumerable<ZafiroPath> filePaths)
    //{
    //    return filePaths
    //        .Select(x => x.Parent())
    //        .Distinct()
    //        .Select(x => x.Parents().Append(x))
    //        .SelectMany(x => x);
    //}
}
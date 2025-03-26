using System.IO.Abstractions;
using Zafiro.DivineBytes;
using IDirectory = Zafiro.DivineBytes.IDirectory;

namespace MsixPackaging.Tests.Helpers;

internal class IODir(IDirectoryInfo directoryInfo) : IDirectory
{
    public string Name => directoryInfo.Name;
    public IEnumerable<INamed> Children => directoryInfo
        .GetFiles()
        .Select(info => new IOFile(info))
        .Concat<INamed>(directoryInfo
            .GetDirectories()
            .Select(info => new IODir(info)));
}
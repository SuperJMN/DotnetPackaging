using System.IO.Abstractions;
using Zafiro.DivineBytes;

namespace MsixPackaging.Tests.Helpers;

internal class IODir(IDirectoryInfo directoryInfo) : IContainer
{
    public string Name => directoryInfo.Name;
    public IEnumerable<INamed> Children => directoryInfo
        .GetFiles()
        .Select(info => new IOFile(info))
        .Concat<INamed>(directoryInfo
            .GetDirectories()
            .Select(info => new IODir(info)));
}
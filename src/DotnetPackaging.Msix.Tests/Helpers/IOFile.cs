using System.IO.Abstractions;
using CSharpFunctionalExtensions;
using Zafiro.DivineBytes;

namespace MsixPackaging.Tests.Helpers;

internal class IOFile : INamedByteSource
{
    private readonly IFileInfo fileInfo;

    public IOFile(IFileInfo info)
    {
        fileInfo = info;
        Source = ByteSource.FromStreamFactory(info.OpenRead, async () => info.Length);
    }

    public IByteSource Source { get; }

    public string Name => fileInfo.Name;
    public IDisposable Subscribe(IObserver<byte[]> observer)
    {
        return Source.Subscribe(observer);
    }

    public IObservable<byte[]> Bytes => Source.Bytes;
}
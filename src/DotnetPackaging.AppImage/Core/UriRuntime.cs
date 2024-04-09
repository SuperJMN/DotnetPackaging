using System.Runtime.InteropServices;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage.Core;

public class UriRuntime : IRuntime
{
    private readonly Architecture architecture;

    public UriRuntime(Architecture architecture)
    {
        this.architecture = architecture;
    }

    public Func<Task<Result<Stream>>> StreamFactory => () => RuntimeDownloader.GetRuntimeStream(architecture, new DefaultHttpClientFactory());
}
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Model;

public class CustomAppImage : AppImageBase
{
    public CustomAppImage(IRuntime runtime, Application application) : base(runtime)
    {
        Application = application;
    }

    public Application Application { get; }
    public override Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> PayloadEntries() => GetPayload(Application);

    private async Task<Result<IEnumerable<(ZafiroPath Path, IBlob Blob)>>> GetPayload(Application application)
    {
        entries();
        throw new NotImplementedException();
    }

    private IEnumerable<(ZafiroPath Path, IBlob Blob)> entries()
    {
        yield return (ZafiroPath.Empty, new Blob("AppRun", Application.AppRun.StreamFactory));
    }
}
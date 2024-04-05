namespace DotnetPackaging.AppImage.Model;

public class AppImage
{
    public AppImage(IRuntime runtime, Application application)
    {
        Runtime = runtime;
        Application = application;
    }

    public IGetStream Runtime { get; }
    public Application Application { get; }
}

public interface IRuntime : IGetStream
{
}
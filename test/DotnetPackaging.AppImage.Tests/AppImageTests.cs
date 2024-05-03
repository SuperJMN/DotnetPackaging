using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public void Test()
    {
        //var appImage = new AppImage(new FakeRuntime(), new Application()
        //{
        //    Metadata = new PackageMetadata(),
        //    Contents = new Root();
        //});
    }
}

public class AppImage
{
    public IRuntime Runtime { get; }

    public AppImage(IRuntime runtime, IApplication appContents)
    {
        Runtime = runtime;
    }
}


public interface IApplication
{
    public PackageMetadata Metadata { get; set; }
    public IRoot Contents { get; set; }
}

public interface IBoostrapper
{
}

public interface IRuntime
{
}

public class Application : IApplication
{
    public PackageMetadata Metadata { get; set; }
    public IRoot Contents { get; set; }
}

public interface IRoot : IDirectory
{
}

public class FakeRuntime : IRuntime
{
}
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Tests;

public class AppImageTests
{
    [Fact]
    public void Test()
    {
        var builder = new DebFileBuilder()
            .FromDirectory(new RegularDirectory("mama"))
            .Configure(setup => setup
                .Package("AvaloniaSyncer")
                .PackageId("com.SuperJMN.AvaloniaSyncer")
                .ExecutableName("AvaloniaSyncer.Desktop"))
            .Build();
    }
}

public class DebFileBuilder
{
    public FromContainerOptions FromDirectory(IDirectory directory)
    {
        return new FromContainerOptions(directory);
    }
}

public class ContainerOptionsSetup
{
    private string package;
    private string packageId;
    private string executableName;

    public ContainerOptionsSetup Package(string package)
    {
        this.package = package;
        return this;
    }
    
    public ContainerOptionsSetup PackageId(string packageId)
    {
        this.packageId = packageId;
        return this;

    }

    public ContainerOptionsSetup ExecutableName(string executableName)
    {
        this.executableName = executableName;
        return this;
    }
}

public class FromContainerOptions
{
    private readonly IDirectory container;

    public FromContainerOptions(IDirectory container)
    {
        this.container = container;
    }

    public FromContainer Configure(Action<ContainerOptionsSetup> setup)
    {
        var options = new ContainerOptionsSetup();
        setup(options);
        return new FromContainer(container, options);
    }
}

public class FromContainer
{
    private readonly IDirectory directory;
    private readonly ContainerOptionsSetup setup;

    public FromContainer(IDirectory directory, ContainerOptionsSetup setup)
    {
        this.directory = directory;
        this.setup = setup;
    }

    public AppImage Build()
    {
        return new AppImage(new FakeRuntime(), new Application());
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
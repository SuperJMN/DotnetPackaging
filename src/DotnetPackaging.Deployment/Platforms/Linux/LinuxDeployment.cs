using DotnetPackaging.AppImage;
using DotnetPackaging.AppImage.Core;
using DotnetPackaging.AppImage.Metadata;

namespace DotnetPackaging.Deployment.Platforms.Linux;

public class LinuxDeployment(IDotnet dotnet, string projectPath, AppImageMetadata metadata, Maybe<ILogger> logger)
{
    private static readonly Dictionary<Architecture, (string Runtime, string RuntimeLinux)> LinuxArchitecture = new()
    {
        [Architecture.X64] = ("linux-x64", "x86_64"),
        [Architecture.Arm64] = ("linux-arm64", "arm64")
    };

    public Task<Result<IEnumerable<INamedByteSource>>> Create()
    {
        IEnumerable<Architecture> supportedArchitectures = [Architecture.Arm64, Architecture.X64];

        return supportedArchitectures
            .Select(architecture => CreateAppImage(architecture))
            .CombineInOrder();
    }

    private Task<Result<INamedByteSource>> CreateAppImage(Architecture architecture)
    {
        var publishOptions = new[]
        {
            new[] { "configuration", "Release" },
            new[] { "runtime", LinuxArchitecture[architecture].Runtime },
            new[] { "self-contained", "true" }
        };

        var arguments = ArgumentsParser.Parse(publishOptions, []);

        var appImageFilename = metadata.PackageName + "-" + metadata.Version.GetValueOrDefault("1.0.0") + "-" + LinuxArchitecture[architecture].RuntimeLinux + ".AppImage";

        return dotnet.Publish(projectPath, arguments)
            .Bind(container => new AppImageFactory().Create(container, metadata))
            .Bind(container => container.ToByteSource())
            .Map(INamedByteSource (byteSource) => new Resource(appImageFilename, byteSource));
    }
}
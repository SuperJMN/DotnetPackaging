using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Publish;
using Serilog;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb;

public static class DebPackagerExtensions
{
    public static IByteSource FromProject(
        this DebPackager packager,
        string projectPath,
        Action<FromDirectoryOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        if (packager == null)
        {
            throw new ArgumentNullException(nameof(packager));
        }

        var options = new FromDirectoryOptions();
        configure?.Invoke(options);

        var publishRequest = new ProjectPublishRequest(projectPath);
        publishConfigure?.Invoke(publishRequest);

        var log = logger ?? Log.Logger;
        var publisher = new DotnetPublisher(Maybe<ILogger>.From(logger));
        var context = ProjectPackagingContext.FromProject(projectPath, log);
        if (context.IsFailure)
        {
            return PackagingByteSource.FromFailure(context.Error);
        }

        return ByteSource.FromDisposableAsync(
            () => publisher.Publish(publishRequest),
            container => packager.FromPublishedProject(container, context.Value, options, log));
    }

    public static IByteSource FromPublishedProject(
        this DebPackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        FromDirectoryOptions? overrides = null,
        ILogger? logger = null)
    {
        if (packager == null) throw new ArgumentNullException(nameof(packager));
        if (publishedProject == null) throw new ArgumentNullException(nameof(publishedProject));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var log = logger ?? Log.Logger;
        var resolved = context.ResolveFromDirectoryOptions(overrides ?? new FromDirectoryOptions());
        return PackagingByteSource.FromResultFactory(() => packager.Pack(publishedProject, resolved, log));
    }

    public static Task<Result> PackPublishedProject(
        this DebPackager packager,
        IContainer publishedProject,
        ProjectPackagingContext context,
        string outputPath,
        FromDirectoryOptions? overrides = null,
        ILogger? logger = null)
    {
        var source = packager.FromPublishedProject(publishedProject, context, overrides, logger);
        return source.WriteTo(outputPath);
    }

    public static Task<Result> PackProject(
        this DebPackager packager,
        string projectPath,
        string outputPath,
        Action<FromDirectoryOptions>? configure = null,
        Action<ProjectPublishRequest>? publishConfigure = null,
        ILogger? logger = null)
    {
        var source = packager.FromProject(projectPath, configure, publishConfigure, logger);
        return source.WriteTo(outputPath);
    }
}

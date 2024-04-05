using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem;

namespace DotnetPackaging.AppImage;

[SuppressMessage("ReSharper", "ParameterHidesMember")]
public class AppImageBuilder
{
    private string applicationName = "Application";
    private string applicationExecutableName = "Application.Desktop";
    private Maybe<IIcon> icon;

    private DesktopMetadata desktopMetadata = new()
    {
        Categories = [],
        Comment = "",
        Name = "Application",
        Keywords = [],
        StartupWmClass = "",
    };

    public Task<Result<Model.AppImage>> Build(IZafiroDirectory contents, IRuntime runtime)
    {
        return Result.Success()
            .Map(() => contents)
            .Check(directory => directory.GetFile(applicationExecutableName).MapError(_ => $"Cannot find {applicationExecutableName} inside the contents. Please check if the name is correct."))
            .Map(directory =>
            {
                var script = $"#!/usr/bin/env sh\n\"$APPDIR/usr/bin/{applicationName}/{applicationExecutableName}\" \"$@\"";
                var application = new Application(directory, icon.GetValueOrDefault(new DefaultIcon()), desktopMetadata, new ScriptAppRun(script));
                return new Model.AppImage(runtime, application);
            });
    }

    public AppImageBuilder WithAppName(string appName)
    {
        this.applicationName = appName;
        return this;
    }

    public AppImageBuilder WithIcon(IIcon icon)
    {
        this.icon = Maybe.From(icon);
        return this;
    }
    public AppImageBuilder WithApplicationExecutableName(string applicationExecutableName)
    {
        this.applicationExecutableName = applicationExecutableName;
        return this;
    }

    public AppImageBuilder WithDesktopMetadata(DesktopMetadata desktopMetadata)
    {
        this.desktopMetadata = desktopMetadata;
        return this;
    }
}
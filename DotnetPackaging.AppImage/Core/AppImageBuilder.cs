using System.Diagnostics.CodeAnalysis;
using ClassLibrary1;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;

namespace DotnetPackaging.AppImage;

[SuppressMessage("ReSharper", "ParameterHidesMember")]
public class AppImageBuilder
{
    private Maybe<IIcon> icon;

    private Maybe<DesktopMetadata> desktopMetadata = Maybe.None;

    public async Task<Result<Model.AppImage>> Build(IDataTree contents, IRuntime runtime, IAppRun appRun)
    {
        return Result.Success()
            .Map(() => contents)
            .Map(contents =>
            {
                var application = new Application(contents, icon.GetValueOrDefault(new DefaultIcon()), desktopMetadata, appRun);
                return new Model.AppImage(runtime, application);
            });
    }

    public AppImageBuilder WithIcon(IIcon icon)
    {
        this.icon = Maybe.From(icon);
        return this;
    }

    public AppImageBuilder WithDesktopMetadata(DesktopMetadata desktopMetadata)
    {
        this.desktopMetadata = Maybe.From(desktopMetadata);
        return this;
    }
}
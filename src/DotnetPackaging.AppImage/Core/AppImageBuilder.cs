using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.AppImage.Core;

[SuppressMessage("ReSharper", "ParameterHidesMember")]
public class AppImageBuilder
{
    //private Maybe<IIcon> icon;

    //private Maybe<DesktopMetadata> desktopMetadata = Maybe.None;

    //public async Task<Result<Model.CustomAppImage>> Build(IBlobContainer contents, IRuntime runtime, IAppRun appRun)
    //{
    //    return Result.Success()
    //        .Map(() => contents)
    //        .Map(contents =>
    //        {
    //            var application = new Application(contents, icon, desktopMetadata, appRun);
    //            return new Model.CustomAppImage(runtime, application);
    //        });
    //}

    //public AppImageBuilder WithIcon(IIcon icon)
    //{
    //    this.icon = Maybe.From(icon);
    //    return this;
    //}

    //public AppImageBuilder WithDesktopMetadata(DesktopMetadata desktopMetadata)
    //{
    //    this.desktopMetadata = Maybe.From(desktopMetadata);
    //    return this;
    //}
}
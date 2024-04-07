using ClassLibrary1;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Model;
using NyaFs.Filesystem.SquashFs;
using NyaFs.Filesystem.SquashFs.Types;

namespace DotnetPackaging.AppImage.Core;

public class AppImageWriter
{
    public static Task<Result> Write(MemoryStream stream, Model.AppImage appImage)
    {
        return appImage.Runtime.WriteTo(stream)
            .Bind(() =>
        {
            return WritePayload(stream, appImage.Application);
        });
    }

    private static Task<Result> WritePayload(MemoryStream stream, Application appImageApplication)
    {
        var payload = GetPayload(appImageApplication);
        return SquashFS.Write(stream, payload);
    }

    private static IDataTree GetPayload(Application application)
    {
        //new InMemoryDataTree(new List<IData>()
        //{
        //    "AppRun"
        //});
        throw new NotImplementedException();
    }
}
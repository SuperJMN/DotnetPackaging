using CSharpFunctionalExtensions;
using System.Reactive.Linq;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging;

public static class ZafiroMixin
{
    public static IObservable<T> Flatten<T>(this IObservable<IObservable<T>> enumerable)
    {
        return enumerable.SelectMany(x => x);
    }
    
    public static IObservable<T> Flatten<T>(this IObservable<IEnumerable<T>> enumerable)
    {
        return enumerable.SelectMany(x => x);
    }
    
    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> enumerable)
    {
        return enumerable.SelectMany(x => x);
    }
}

public record PackageMetadata
{
    public string AppName { get; init; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; init; }
    public Maybe<string> Comment { get; init; }
    public Maybe<Categories> Categories { get; init; }
    public Maybe<IIcon> Icon { get; init; }
    public Maybe<string> Version { get; init; }
    public Maybe<Uri> HomePage { get; init; }
    public Maybe<IEnumerable<Uri>> ScreenshotUrls { get; init; }
    public Maybe<string> Summary { get; init; }
    public Maybe<string> License { get; init; }
    public Maybe<string> AppId { get; init; }
    public string Package { get; set; }
    public string Section { get; set; }
    public string Priority { get; set; }
    public string Architecture { get; set; }
    public string Maintainer { get; set; }
    public string Description { get; set; }
    public string Homepage { get; set; }
    public string Recommends { get; set; }
    public string VcsGit { get; set; }
    public string VcsBrowser { get; set; }
    public string InstalledSize { get; set; }
    public DateTimeOffset ModificationTime { get; set; }
    public string ExecutableName { get; set; }
}

public interface IIcon : IByteProvider;
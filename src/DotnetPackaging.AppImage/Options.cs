using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.AppImage;

public class Options
{
    public Maybe<string> AppName { get; set; }
    public Maybe<string> StartupWmClass { get; set; }
    public Maybe<IEnumerable<string>> Keywords { get; set; }
    public Maybe<string> Comment { get; set; }
    public Maybe<IEnumerable<string>> Categories { get; set; }
    public Maybe<IIcon> Icon { get; set; }
}
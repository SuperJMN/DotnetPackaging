using System.Reactive.Linq;
using DotnetPackaging.Common;
using DotnetPackaging.Deb;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

public static class TestData
{
    public static Metadata Metadata() => new()
    {
        PackageName = "SamplePackage",
        Description = "This is a sample",
        ApplicationName = "My application",
        Architecture = "amd64",
        Homepage = "https://www.something.com",
        License = "MIT",
        Maintainer = "Me"
    };

    public static Contents Contents()
    {
        var resources = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable())));

        var contents = new Contents
        {
            new RegularContent("Contenido1.txt", () => "Soy pepito".GetAsciiBytes().ToObservable()),
            new RegularContent("Contenido2.txt", () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()),
            new ExecutableContent("Program.Desktop", () => "Binary data. Irrelevant for the test.".GetAsciiBytes().ToObservable(), resources.Value)
            {
                Name = "Program",
                StartupWmClass = "My program",
            },
        };
        return contents;
    }
}
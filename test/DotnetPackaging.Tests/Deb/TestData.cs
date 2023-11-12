using System.Reactive.Linq;
using DotnetPackaging.Common;
using Zafiro.IO;

namespace DotnetPackaging.Tests.Deb;

// TODO: Place some interesting tests here
//public static class TestData
//{
//    public static Metadata Metadata() => new()
//    {
//        PackageName = "SamplePackage",
//        Description = "This is a sample",
//        ApplicationName = "My application",
//        Architecture = "amd64",
//        Homepage = "https://www.something.com",
//        License = "MIT",
//        Maintainer = "Me",
//        Version = "0.0.1"
//    };

//    public static Contents Contents()
//    {
//        var desktopEntry = DesktopEntry();

//        var contents = new Contents
//        {
//            new RegularContent("Contenido1.txt", () => "Soy pepito".GetAsciiBytes().ToObservable()),
//            new RegularContent("Contenido2.txt", () => "Dale, Don, dale.".GetAsciiBytes().ToObservable()),
//            new ExecutableContent("Program.Desktop", () => "Binary data. Irrelevant for the test.".GetAsciiBytes().ToObservable())
//            { 
//                DesktopEntry = desktopEntry,
//                CommandName = "myprogram",
//            }
//        };
//        return contents;
//    }

//    public static DesktopEntry DesktopEntry()
//    {
//        var desktopEntry = new DesktopEntry
//        {
//            Name = "Test Program",
//            Icons = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead("TestFiles\\icon.png"), stream => stream.ToObservable()))).Value,
//            StartupWmClass = "My program",
//            Keywords = new[] { "test" },
//            Categories = new[] { "Utilities" },
//            Comment = "This is just a test"
//        };
//        return desktopEntry;
//    }
//}
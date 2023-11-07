// See https://aka.ms/new-console-template for more information

using System.Reactive.Linq;
using DotnetPackaging;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

Console.WriteLine("Hello, World!");

var iconPath = @"c:\users\jmn\desktop\testing\vinay\icon.png";
var iconResources = IconResources.Create(new IconData(32, () => Observable.Using(() => File.OpenRead(iconPath), s => s.ToObservable()))).Value;

Dictionary<ZafiroPath, ExecutableMetadata> execs = new Dictionary<ZafiroPath, ExecutableMetadata>()
{
    ["TestDebpackage.Desktop"] = new ExecutableMetadata("vinaytest", new DesktopEntry()
    {
        Categories = new List<string>() { "Financial"},
        Comment = "This is a test",
        Icons = iconResources,
        Keywords = new []{ "Sample "},
        Name = "Sample application",
        StartupWmClass = "Sample"
    })
};

Metadata metadata = new Metadata()
{
    ApplicationName = "Sample",
    Architecture = "amd64",
    Description = "Sample",
    Homepage = "https://www.sample.com",
    License = "MIT",
    Maintainer = "SuperJMN",
    PackageName = "SamplePackage",
    Version = "1.0.0",
};

var result = await Create.Deb(@"c:\users\jmn\desktop\testing\vinay", @"c:\users\jmn\desktop\testing\vinay.deb", metadata, execs);
Console.WriteLine(result.IsSuccess);
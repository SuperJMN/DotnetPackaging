// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.Reactive.Linq;
using DotnetPackaging;
using DotnetPackaging.Deb;
using Zafiro.FileSystem;
using Zafiro.IO;

var fileOption = new Option<DirectoryInfo>(name: "--directory", description: "The input directory to create the package from");

var rootCommand = new RootCommand("Sample app for System.CommandLine");
rootCommand.AddOption(fileOption);

rootCommand.SetHandler(ReadFile, fileOption);

return await rootCommand.InvokeAsync(args);

static async Task ReadFile(DirectoryInfo directory)
{
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

    await Create.Deb(directory.FullName, @"c:\users\jmn\desktop\testing\vinay.deb", metadata, execs);
}
# Distribute your aplications!

Wondering how to distribute your wonderful .NET application to your fellow peeps? Bored to pack your stuff in a .zip that you hate as much as your users? You are in the correct place!

# Mission

DotnetPackage has been created to give an answer to those that want to distribute their applications in a more convenient way. 

One of the most flagrant annoyances of the .NET world is the absence of standardized ways to distribute "classic" applications, those that are for end users. Eg. You have an [Avalonia UI](https://www.avaloniaui.net) application you want to pack for Linux. You just need to install a .NET tool.

```powershell
dotnet tool install --global DotnetPackaging.Console
```

After the tool is installed, just invoke it with the appropirate arguments:

Create you .deb packages for Debian based systems like Ubuntu and Debian itself.

```powershell
dotnetpackaging --directory c:\repos\myapp\bin\Release\net7.0\publish\linux-x64 --metadata C:\Users\JMN\Desktop\Testing\metadata.deb.json --output c:\users\jmn\desktop\testing\myapp.1.0.0.x64.deb
```

# Metadata.deb.json

You need to provide the metadata of your .deb package. Example here!

```json
{
    "Executables": {
      "MyApp.Desktop": {
        "CommandName": "myapp",
        "DesktopEntry": {
          "Icons": {
            "32": "C:\\Users\\JMN\\Desktop\\Testing\\icon32.png",
            "64": "C:\\Users\\JMN\\Desktop\\Testing\\icon64.png"
          },
          "Name": "Sample application",
          "StartupWmClass": "Sample",
          "Keywords": [
            "Sample"
          ],
          "Comment": "This is a test",
          "Categories": [
            "Financial"
          ]
        }
      }
    },
    "PackageMetadata": {
      "Maintainer": "Some avid programmer",
      "PackageName": "SamplePackage",
      "ApplicationName": "Sample",
      "Architecture": "amd64",
      "Homepage": "https://www.sample.com",
      "License": "MIT",
      "Description": "Sample",
      "Version": "1.0.0"
    }
  }
```

# Nuke Build system

This is not only to use as a command line tool. We have you covered.

Add the **DotnetPackaging**  Nuget package to your Nuke project and use a like like this:

```
var desktopEntry = new DesktopEntry()
{
	Name = "Avalonia Syncer",
	Icons = IconResources.Create(new IconData(32, await Image.LoadAsync("myapp.png"))).Value,
	StartupWmClass = "AvaloniaSyncer",
	Keywords = new[] { "file manager" },
	Comment = "The best file explorer ever",
	Categories = new[] { "FileManager", "Filesystem", "Utility", "FileTransfer", "Archiving" }
};

var metadata = new Metadata
{
	PackageName = "AvaloniaSyncer",
	Description = "Best file explorer you'll ever find",
	ApplicationName = "Avalonia Syncer",
	Architecture = "amd64",
	Homepage = "https://www.something.com",
	License = "MIT",
	Maintainer = "SuperJMN@outlook.com",
	Version = "0.1.33"
};

var executableFiles = new Dictionary<ZafiroPath, ExecutableMetadata>
{
	["MyApp.Desktop"] = new("avaloniasyncer", desktopEntry),    // You need this to **mark** a given file as executable. It created the desktop entry with its icons and so.
};

await Create.Deb(@"c:\repos\myapp\bin\Release\net7.0\publish\linux-x64", "output/myapp.deb", metadata, executableFiles);
```

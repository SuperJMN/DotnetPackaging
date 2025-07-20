# Distribute your aplications!

Wondering how to distribute your wonderful .NET application to your fellow peeps? Bored to pack your stuff in a .zip that you hate as much as your users? You are in the correct place!
With it, you can create your .Deb and AppImage packages for Linux systems like Ubuntu, Debian and others. Woohoo!

# Overview

One of the most flagrant annoyances of the .NET world is the absence of standardized ways to distribute "classic" applications, those that are for end users. Imagine that you have a beautiful cross-platform application, like the ones created using [Avalonia UI](https://www.avaloniaui.net). Everything is fine until you need to distribute you apps. 

**DotnetPackaging** has been created to give an answer to those that want to distribute their applications in a more convenient way. It's about time, uh?

# How can I use this?

The application can be used as a library or as a .NET tool.

# Using it as a .NET Tool

Easy peasy. Install the tool by executing this command:

```powershell
dotnet tool install --global DotnetPackaging.Console
```

After the tool is installed, just invoke it with the appropriate arguments.

## Deploying with dotnetdeployer

The repository also includes a CLI called `dotnetdeployer`. It automates the
publishing of NuGet packages and GitHub releases so it can be easily invoked
from CI pipelines like Azure DevOps.

The Azure Pipeline determines the version using [GitVersion](https://gitversion.net),
so releases automatically follow the Git history.

You can publish the tool itself using a single command:

```powershell
dotnet run --project src/DotnetPackaging.DeployerTool/DotnetPackaging.DeployerTool.csproj -- \
    nuget --project src/DotnetPackaging.DeployerTool/DotnetPackaging.DeployerTool.csproj \
    --version 1.0.0 --api-key <YOUR_NUGET_API_KEY>
```

If no `--project` is supplied, `dotnetdeployer` automatically discovers all packable projects from the specified solution (default `DotnetPackaging.sln`) and publishes them, ignoring test, sample or desktop projects.

## Samples

### AppImage

This tool can create [AppImage](https://appimage.org) packages. Just invoke it this way:

```csharp
dotnetpackaging appimage ... <options>
```

## From single directory

You can create them using a single build directory (using dotnet publish, for example). The tool will use the binaries and resources in that directory to create the AppImage. 

## From AppImage

You can create a directory structure according to the [AppDir specs](https://docs.appimage.org/reference/appdir.html) and use this tool to create the package from it. 

### Deb files

This is a sample to create a **deb** package.

```powershell
dotnetpackaging deb --directory c:\repos\myapp\bin\Release\net7.0\publish\linux-x64 --metadata C:\Users\JMN\Desktop\Testing\metadata.deb.json --output c:\users\jmn\desktop\testing\myapp.1.0.0.x64.deb
```

- Wait, wait! I understand the --directory and --output options, but what's the json file?

it's a special file that you need to edit to customize the properties of your package.

## Metadata.deb.json

This is a sample file. Customize it to your needs.

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

# Integrate it with Nuke Build system

AS I mentioned above, DotnetPackaging isn't just a tool, but a library, too. You can integrate it with [Nuke](https://nuke.build) very easily!

Take a looks to this example:

1. https://github.com/SuperJMN/AvaloniaSyncer/blob/26260a6d2cc7c611d60e5c1e5b821f8512877ba5/build/Build.cs#L46
2. https://github.com/SuperJMN/AvaloniaSyncer/blob/26260a6d2cc7c611d60e5c1e5b821f8512877ba5/build/DebPackages.cs#L16

Feel free to ask in the Discussions section of this repo.
If this has been useful for you, please consider sponsoring this project. Thanks!

# Acknowledgements
- Huge thanks [Alexey Sonkin](https://github.com/teplofizik) for his wonderful SquashFS support in his [NyaFS](https://github.com/teplofizik/nyafs) library.

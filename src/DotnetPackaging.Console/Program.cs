using System.CommandLine;
using CSharpFunctionalExtensions;
using DotnetPackaging;
using DotnetPackaging.Client.Dtos;
using DotnetPackaging.Common;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var contentDir = new Option<DirectoryInfo>(name: "--directory", description: "The input directory to create the package from") { IsRequired = true };
var metadata = new Option<FileInfo>(name: "--metadata", description: "The metadata to include in the package") { IsRequired = true };
var debFile = new Option<FileInfo>(name: "--output", description: "Output file (.deb)") { IsRequired = true };

var rootCommand = new RootCommand("Sample app for System.CommandLine");
rootCommand.AddOption(contentDir);
rootCommand.AddOption(debFile);
rootCommand.AddOption(metadata);

rootCommand.SetHandler(CreateDeb, contentDir, debFile, metadata);

return await rootCommand.InvokeAsync(args);

static async Task CreateDeb(DirectoryInfo contents, FileInfo debFile, FileInfo metadataFile)
{
    var packagingDto = await metadataFile.ToPackageDefinition();
    var packaging = packagingDto.ToModel();
    Log.Logger.Information("Creating {Deb} from {Contents}", debFile.FullName, contents.FullName);
    Log.Logger.Verbose("Metadata for {Deb} is set to {Metadata}", debFile.FullName, packagingDto);
    var result = await Create.Deb(packaging, contents.FullName, debFile.FullName);

    result
        .Tap(() => Log.Information("Success"))
        .TapError(Log.Error);
}
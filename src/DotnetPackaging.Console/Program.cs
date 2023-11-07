using System.CommandLine;
using DotnetPackage.Console;
using DotnetPackaging;

var input = new Option<DirectoryInfo>(name: "--directory", description: "The input directory to create the package from") { IsRequired = true };
var metadata = new Option<FileInfo>(name: "--metadata", description: "The metadata to include in the package") { IsRequired = true };
var output = new Option<FileInfo>(name: "--output", description: "Output file (.deb)") { IsRequired = true };

var rootCommand = new RootCommand("Sample app for System.CommandLine");
rootCommand.AddOption(input);
rootCommand.AddOption(output);
rootCommand.AddOption(metadata);

rootCommand.SetHandler(CreateDeb, input, output, metadata);

return await rootCommand.InvokeAsync(args);

static async Task CreateDeb(DirectoryInfo input, FileInfo output, FileInfo metadataFile)
{
    var packaging = await Packaging.FromFile(metadataFile);
    await Create.Deb(input.FullName, output.FullName, packaging.Metadata, packaging.ExecutableMappings);
}
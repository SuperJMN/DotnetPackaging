using CSharpFunctionalExtensions;
using DotnetPackaging.Dmg;
using Zafiro.DivineBytes.System.IO;
using Zafiro.DivineBytes;

Console.WriteLine("Generating Output.dmg...");

var startDir = Directory.GetCurrentDirectory();
var sourceDir = Path.Combine(startDir, "src/DotnetPackaging.Dmg.Reproduce");
var outputDmg = Path.Combine(startDir, "Output.dmg");

// Create a dummy file to ensure we have content
var testFile = Path.Combine(sourceDir, "test.txt");
File.WriteAllText(testFile, "Hello World from DMG Reproduction");

var container = new DirectoryContainer(new System.IO.Abstractions.FileSystem().DirectoryInfo.New(sourceDir)).AsRoot();
var metadata = new DmgPackagerMetadata
{
    VolumeName = Maybe.From("TestImage"),
    Compress = Maybe.From(false)
};

var packager = new DmgPackager();
var result = await packager.Pack(container, metadata);
if (result.IsFailure)
{
    Console.WriteLine($"Failed to build DMG: {result.Error}");
    return;
}

await result.Value.WriteTo(outputDmg);

Console.WriteLine($"Generated: {outputDmg}");

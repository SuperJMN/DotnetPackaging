using DotnetPackaging.Dmg;

Console.WriteLine("Generating Output.dmg...");

var startDir = Directory.GetCurrentDirectory();
var sourceDir = Path.Combine(startDir, "src/DotnetPackaging.Dmg.Reproduce");
var outputDmg = Path.Combine(startDir, "Output.dmg");

// Create a dummy file to ensure we have content
var testFile = Path.Combine(sourceDir, "test.txt");
File.WriteAllText(testFile, "Hello World from DMG Reproduction");

await DmgHfsBuilder.Create(
    sourceDir, 
    outputDmg, 
    "TestImage", 
    compress: false // Start uncompressed to simplify debugging
);

Console.WriteLine($"Generated: {outputDmg}");

using System.IO.Compression;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using DotnetPackaging.Msix.Core.Manifest;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using System.IO.Abstractions;

namespace MsixPackaging.Tests;

/// <summary>
/// Full end-to-end test: takes a directory, packages it with all Store-ready
/// features (metadata, signing, icon-based asset generation), then extracts
/// the resulting MSIX and validates every aspect cross-platform.
/// </summary>
public class StoreReadyE2eTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Mp = "http://schemas.microsoft.com/appx/2014/phone/manifest";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    private static readonly XNamespace Rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";
    private static readonly XNamespace BlockMapNs = "http://schemas.microsoft.com/appx/2010/blockmap";
    private static readonly XNamespace ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    public StoreReadyE2eTests(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .Enrich.FromLogContext()
            .CreateLogger();
    }

    [Fact]
    public async Task Full_Store_ready_package_passes_all_validations()
    {
        // Arrange: create a 256x256 test icon
        using var icon = new Image<Rgba32>(256, 256, new Rgba32(66, 133, 244));
        using var iconMs = new MemoryStream();
        icon.SaveAsPng(iconMs);
        var iconBytes = iconMs.ToArray();

        var metadata = new AppManifestMetadata
        {
            Name = "com.e2etest.storeready",
            Publisher = "CN=E2E-Test-Publisher",
            Version = "2.0.1.0",
            ProcessorArchitecture = "x64",
            DisplayName = "E2E Store Ready App",
            PublisherDisplayName = "E2E Test Publisher",
            AppId = "StoreReadyApp",
            Executable = "HelloWorld.exe",
            AppDisplayName = "E2E Store Ready App",
            AppDescription = "An end-to-end test for Store-ready MSIX packaging",
            ShortName = "E2EApp",
            BackgroundColor = "#4285F4",
            MinVersion = "10.0.17763.0",
            MaxVersionTested = "10.0.22621.0",
        };

        var signing = new SigningOptions { PublisherCN = "CN=E2E-Test-Publisher" };

        var fs = new FileSystem();
        var dirInfo = fs.DirectoryInfo.New("TestFiles/MinimalNoMetadata/Contents");
        var container = new DirectoryContainer(dirInfo);
        var packager = new MsixPackager();

        // Act
        var result = await packager.Pack(
            container,
            Maybe.From(metadata),
            Maybe.From(signing),
            Maybe.From(iconBytes),
            Log.Logger);

        Assert.True(result.IsSuccess, $"Packaging failed: {result.IsFailure}");

        using var packageMs = new MemoryStream();
        await result.Value.WriteTo(packageMs);
        packageMs.Position = 0;

        // Extract
        using var zip = new ZipArchive(packageMs, ZipArchiveMode.Read);
        var entries = zip.Entries.Select(e => e.FullName).ToList();

        Log.Logger.Information("Package contains {Count} entries: {Entries}", entries.Count, string.Join(", ", entries));

        // === Validate required entries exist ===
        AssertEntryExists(entries, "AppxManifest.xml");
        AssertEntryExists(entries, "AppxBlockMap.xml");
        AssertEntryExists(entries, "[Content_Types].xml");
        AssertEntryExists(entries, "AppxSignature.p7x");
        AssertEntryExists(entries, "HelloWorld.exe");

        // Generated visual assets
        AssertEntryExists(entries, "Assets/StoreLogo.png");
        AssertEntryExists(entries, "Assets/Square44x44Logo.png");
        AssertEntryExists(entries, "Assets/Square150x150Logo.png");
        AssertEntryExists(entries, "Assets/Wide310x150Logo.png");
        AssertEntryExists(entries, "Assets/Square310x310Logo.png");
        AssertEntryExists(entries, "Assets/SplashScreen.png");

        // === Validate AppxManifest.xml ===
        var manifestXml = await ReadEntryString(zip, "AppxManifest.xml");
        var manifest = XDocument.Parse(manifestXml);
        ValidateManifest(manifest, metadata);

        // === Validate AppxBlockMap.xml ===
        var blockMapXml = await ReadEntryString(zip, "AppxBlockMap.xml");
        var blockMap = XDocument.Parse(blockMapXml);
        ValidateBlockMap(blockMap, entries);

        // === Validate [Content_Types].xml ===
        var contentTypesXml = await ReadEntryString(zip, "[Content_Types].xml");
        var contentTypes = XDocument.Parse(contentTypesXml);
        ValidateContentTypes(contentTypes);

        // === Validate AppxSignature.p7x ===
        var signatureBytes = await ReadEntryBytes(zip, "AppxSignature.p7x");
        ValidateSignature(signatureBytes);

        // === Validate visual asset dimensions ===
        await ValidateAssetDimensions(zip, "Assets/StoreLogo.png", 50, 50);
        await ValidateAssetDimensions(zip, "Assets/Square44x44Logo.png", 44, 44);
        await ValidateAssetDimensions(zip, "Assets/Square150x150Logo.png", 150, 150);
        await ValidateAssetDimensions(zip, "Assets/Wide310x150Logo.png", 310, 150);
        await ValidateAssetDimensions(zip, "Assets/Square310x310Logo.png", 310, 310);
        await ValidateAssetDimensions(zip, "Assets/SplashScreen.png", 620, 300);
    }

    private static void ValidateManifest(XDocument manifest, AppManifestMetadata expected)
    {
        var root = manifest.Root!;
        Assert.Equal("Package", root.Name.LocalName);

        // Identity
        var identity = root.Element(Ns + "Identity")!;
        Assert.Equal(expected.Name, identity.Attribute("Name")!.Value);
        Assert.Equal(expected.Publisher, identity.Attribute("Publisher")!.Value);
        Assert.Equal(expected.Version, identity.Attribute("Version")!.Value);
        Assert.Equal(expected.ProcessorArchitecture, identity.Attribute("ProcessorArchitecture")!.Value);

        // PhoneIdentity
        var phoneId = root.Element(Mp + "PhoneIdentity");
        Assert.NotNull(phoneId);
        Assert.False(string.IsNullOrWhiteSpace(phoneId.Attribute("PhoneProductId")!.Value));

        // Properties
        var props = root.Element(Ns + "Properties")!;
        Assert.Equal(expected.DisplayName, props.Element(Ns + "DisplayName")!.Value);
        Assert.Equal(expected.PublisherDisplayName, props.Element(Ns + "PublisherDisplayName")!.Value);
        Assert.Equal(expected.Logo, props.Element(Ns + "Logo")!.Value);

        // Dependencies: Windows.Desktop with correct versions
        var deps = root.Element(Ns + "Dependencies")!;
        var desktopFamily = deps.Elements(Ns + "TargetDeviceFamily")
            .FirstOrDefault(e => e.Attribute("Name")!.Value == "Windows.Desktop");
        Assert.NotNull(desktopFamily);
        Assert.Equal(expected.MinVersion, desktopFamily.Attribute("MinVersion")!.Value);
        Assert.Equal(expected.MaxVersionTested, desktopFamily.Attribute("MaxVersionTested")!.Value);

        // Application
        var app = root.Element(Ns + "Applications")!.Element(Ns + "Application")!;
        Assert.Equal(expected.AppId, app.Attribute("Id")!.Value);
        Assert.Equal(expected.Executable, app.Attribute("Executable")!.Value);
        Assert.Equal("Windows.FullTrustApplication", app.Attribute("EntryPoint")!.Value);

        // VisualElements
        var visual = app.Element(Uap + "VisualElements")!;
        Assert.Equal(expected.AppDisplayName, visual.Attribute("DisplayName")!.Value);
        Assert.Equal(expected.AppDescription, visual.Attribute("Description")!.Value);
        Assert.Equal(expected.BackgroundColor, visual.Attribute("BackgroundColor")!.Value);

        // DefaultTile
        var tile = visual.Element(Uap + "DefaultTile")!;
        Assert.Equal(expected.Wide310x150Logo, tile.Attribute("Wide310x150Logo")!.Value);
        Assert.Equal(expected.ShortName, tile.Attribute("ShortName")!.Value);

        // SplashScreen
        var splash = visual.Element(Uap + "SplashScreen")!;
        Assert.Equal(expected.SplashScreen, splash.Attribute("Image")!.Value);

        // Capabilities
        var capabilities = root.Element(Ns + "Capabilities")!;
        Assert.Contains(capabilities.Elements(),
            e => e.Name == Ns + "Capability" && e.Attribute("Name")!.Value == "internetClient");
        Assert.Contains(capabilities.Elements(),
            e => e.Name == Rescap + "Capability" && e.Attribute("Name")!.Value == "runFullTrust");
    }

    private static void ValidateBlockMap(XDocument blockMap, IReadOnlyList<string> zipEntries)
    {
        var root = blockMap.Root!;
        Assert.Equal("BlockMap", root.Name.LocalName);
        Assert.Equal("http://www.w3.org/2001/04/xmlenc#sha256", root.Attribute("HashMethod")!.Value);

        var blockMapFiles = root.Elements(BlockMapNs + "File")
            .Select(f => f.Attribute("Name")!.Value)
            .ToList();

        // Block map should contain all payload entries (not itself, not [Content_Types].xml, not AppxSignature.p7x)
        var metaEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AppxBlockMap.xml", "[Content_Types].xml", "AppxSignature.p7x"
        };

        var payloadEntries = zipEntries.Where(e => !metaEntries.Contains(e)).ToList();

        foreach (var entry in payloadEntries)
        {
            // Block map uses backslash paths on Windows but forward slash in ZIP
            var normalizedEntry = entry.Replace('/', '\\');
            Assert.True(
                blockMapFiles.Any(f => f.Equals(entry, StringComparison.OrdinalIgnoreCase) ||
                                       f.Equals(normalizedEntry, StringComparison.OrdinalIgnoreCase)),
                $"Payload entry '{entry}' not found in AppxBlockMap.xml. BlockMap files: {string.Join(", ", blockMapFiles)}");
        }

        // Each file element should have at least one Block child
        foreach (var fileElement in root.Elements(BlockMapNs + "File"))
        {
            var blocks = fileElement.Elements(BlockMapNs + "Block").ToList();
            Assert.True(blocks.Count > 0,
                $"File '{fileElement.Attribute("Name")!.Value}' in BlockMap has no Block elements");

            foreach (var block in blocks)
            {
                Assert.NotNull(block.Attribute("Hash"));
                var hashValue = block.Attribute("Hash")!.Value;
                Assert.False(string.IsNullOrWhiteSpace(hashValue), "Block hash should not be empty");
            }
        }
    }

    private static void ValidateContentTypes(XDocument contentTypes)
    {
        var root = contentTypes.Root!;
        Assert.Equal("Types", root.Name.LocalName);

        var defaults = root.Elements(ContentTypesNs + "Default")
            .ToDictionary(
                e => e.Attribute("Extension")!.Value,
                e => e.Attribute("ContentType")!.Value,
                StringComparer.OrdinalIgnoreCase);

        var overrides = root.Elements(ContentTypesNs + "Override")
            .ToDictionary(
                e => e.Attribute("PartName")!.Value,
                e => e.Attribute("ContentType")!.Value,
                StringComparer.OrdinalIgnoreCase);

        // Verify key mappings are present
        Assert.True(defaults.ContainsKey("exe"), "Missing exe content type");
        Assert.True(defaults.ContainsKey("png"), "Missing png content type");

        // Verify overrides for meta-files
        Assert.True(overrides.ContainsKey("/AppxBlockMap.xml"), "Missing AppxBlockMap.xml override");
        Assert.Equal("application/vnd.ms-appx.blockmap+xml", overrides["/AppxBlockMap.xml"]);
    }

    private static void ValidateSignature(byte[] signatureBytes)
    {
        Assert.True(signatureBytes.Length > 100, $"Signature too small: {signatureBytes.Length} bytes");

        // PKCX magic header
        Assert.Equal(0x50, signatureBytes[0]); // P
        Assert.Equal(0x4B, signatureBytes[1]); // K
        Assert.Equal(0x43, signatureBytes[2]); // C
        Assert.Equal(0x58, signatureBytes[3]); // X

        // After magic, PKCS#7 DER: must start with ASN.1 SEQUENCE tag (0x30)
        Assert.Equal(0x30, signatureBytes[4]);

        // Verify we can parse length (DER long form: 0x82 = 2-byte length)
        Assert.True(signatureBytes[5] == 0x80 || signatureBytes[5] == 0x81 || signatureBytes[5] == 0x82 || signatureBytes[5] == 0x83,
            $"Unexpected DER length byte: 0x{signatureBytes[5]:X2}");
    }

    private static async Task ValidateAssetDimensions(ZipArchive zip, string entryName, int expectedWidth, int expectedHeight)
    {
        var bytes = await ReadEntryBytes(zip, entryName);
        using var img = Image.Load(bytes);
        Assert.Equal(expectedWidth, img.Width);
        Assert.Equal(expectedHeight, img.Height);
    }

    private static void AssertEntryExists(IReadOnlyList<string> entries, string expected)
    {
        Assert.True(entries.Any(e => e.Equals(expected, StringComparison.OrdinalIgnoreCase)),
            $"Expected entry '{expected}' not found in package. Entries: {string.Join(", ", entries)}");
    }

    private static async Task<string> ReadEntryString(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new Exception($"Entry '{entryName}' not found");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task<byte[]> ReadEntryBytes(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new Exception($"Entry '{entryName}' not found");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}

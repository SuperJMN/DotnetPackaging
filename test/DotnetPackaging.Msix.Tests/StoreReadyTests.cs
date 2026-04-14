using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using DotnetPackaging.Msix.Core.Assets;
using DotnetPackaging.Msix.Core.Manifest;
using DotnetPackaging.Msix.Core.Signing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;
using Serilog;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using System.IO.Abstractions;

namespace MsixPackaging.Tests;

public class StoreReadyTests
{
    public StoreReadyTests(ITestOutputHelper output)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .Enrich.FromLogContext()
            .CreateLogger();
    }

    [Fact]
    public void Manifest_contains_all_Store_required_elements()
    {
        var metadata = new AppManifestMetadata
        {
            Name = "com.test.myapp",
            Publisher = "CN=TestPublisher",
            Version = "1.2.3.0",
            ProcessorArchitecture = "x64",
            DisplayName = "My Test App",
            PublisherDisplayName = "Test Publisher Inc.",
            AppId = "MyApp",
            Executable = "MyApp.exe",
            AppDisplayName = "My Test App",
            AppDescription = "A test application",
            ShortName = "MyApp",
            MinVersion = "10.0.17763.0",
            MaxVersionTested = "10.0.22621.0",
        };

        var xml = AppManifestGenerator.GenerateAppManifest(metadata);
        var doc = XDocument.Parse(xml);
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace mp = "http://schemas.microsoft.com/appx/2014/phone/manifest";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        // Identity with ProcessorArchitecture
        var identity = doc.Root!.Element(ns + "Identity")!;
        Assert.Equal("x64", identity.Attribute("ProcessorArchitecture")!.Value);
        Assert.Equal("com.test.myapp", identity.Attribute("Name")!.Value);
        Assert.Equal("CN=TestPublisher", identity.Attribute("Publisher")!.Value);
        Assert.Equal("1.2.3.0", identity.Attribute("Version")!.Value);

        // PhoneIdentity
        var phoneId = doc.Root.Element(mp + "PhoneIdentity");
        Assert.NotNull(phoneId);
        Assert.NotNull(phoneId.Attribute("PhoneProductId")!.Value);

        // TargetDeviceFamily with updated versions
        var deps = doc.Root.Element(ns + "Dependencies")!;
        var desktop = deps.Elements(ns + "TargetDeviceFamily")
            .First(e => e.Attribute("Name")!.Value == "Windows.Desktop");
        Assert.Equal("10.0.17763.0", desktop.Attribute("MinVersion")!.Value);
        Assert.Equal("10.0.22621.0", desktop.Attribute("MaxVersionTested")!.Value);

        // VisualElements with DefaultTile and SplashScreen
        var app = doc.Root.Element(ns + "Applications")!.Element(ns + "Application")!;
        var visual = app.Element(uap + "VisualElements")!;
        Assert.NotNull(visual.Attribute("Square150x150Logo"));
        Assert.NotNull(visual.Attribute("Square44x44Logo"));

        var tile = visual.Element(uap + "DefaultTile")!;
        Assert.NotNull(tile.Attribute("Wide310x150Logo"));
        Assert.Equal("MyApp", tile.Attribute("ShortName")!.Value);

        var splash = visual.Element(uap + "SplashScreen")!;
        Assert.NotNull(splash.Attribute("Image"));
    }

    [Fact]
    public void Asset_generator_produces_all_required_sizes()
    {
        using var icon = new Image<Rgba32>(256, 256);
        using var ms = new MemoryStream();
        icon.SaveAsPng(ms);
        var iconBytes = ms.ToArray();

        var result = MsixAssetGenerator.Generate(iconBytes);
        Assert.True(result.IsSuccess);

        var assets = result.Value;
        Assert.Equal(6, assets.Count);

        AssertAssetSize(assets, @"Assets\StoreLogo.png", 50, 50);
        AssertAssetSize(assets, @"Assets\Square44x44Logo.png", 44, 44);
        AssertAssetSize(assets, @"Assets\Square150x150Logo.png", 150, 150);
        AssertAssetSize(assets, @"Assets\Wide310x150Logo.png", 310, 150);
        AssertAssetSize(assets, @"Assets\Square310x310Logo.png", 310, 310);
        AssertAssetSize(assets, @"Assets\SplashScreen.png", 620, 300);
    }

    [Fact]
    public void Certificate_provider_generates_self_signed()
    {
        var result = CertificateProvider.Get(
            Maybe<string>.None,
            Maybe<string>.None,
            "CN=TestPublisher");

        Assert.True(result.IsSuccess);
        var cert = result.Value;
        Assert.Contains("CN=TestPublisher", cert.Subject);
        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void Signer_produces_valid_AppxSignature()
    {
        var certResult = CertificateProvider.Get(
            Maybe<string>.None,
            Maybe<string>.None,
            "CN=TestPublisher");
        Assert.True(certResult.IsSuccess);

        var blockMapXml = "<BlockMap xmlns=\"http://schemas.microsoft.com/appx/2010/blockmap\" HashMethod=\"http://www.w3.org/2001/04/xmlenc#sha256\" />";
        var blockMapBytes = System.Text.Encoding.UTF8.GetBytes(blockMapXml);

        var signResult = MsixSigner.Sign(blockMapBytes, certResult.Value);
        Assert.True(signResult.IsSuccess);

        var signature = signResult.Value;
        // PKCX magic
        Assert.Equal(0x50, signature[0]);
        Assert.Equal(0x4B, signature[1]);
        Assert.Equal(0x43, signature[2]);
        Assert.Equal(0x58, signature[3]);
        // After magic should be PKCS#7 DER (starts with SEQUENCE tag 0x30)
        Assert.Equal(0x30, signature[4]);
    }

    [Fact]
    public async Task Signed_package_contains_AppxSignature_entry()
    {
        var fs = new FileSystem();
        var dirInfo = fs.DirectoryInfo.New("TestFiles/MinimalNoMetadata/Contents");
        var container = new DirectoryContainer(dirInfo);

        var metadata = new AppManifestMetadata
        {
            Publisher = "CN=TestSign",
            Executable = "HelloWorld.exe",
        };

        var signing = new SigningOptions
        {
            PublisherCN = "CN=TestSign",
        };

        var packager = new MsixPackager();
        var result = await packager.Pack(
            container,
            Maybe.From(metadata),
            Maybe.From(signing),
            logger: Log.Logger);
        Assert.True(result.IsSuccess);

        using var ms = new MemoryStream();
        await result.Value.WriteTo(ms);
        ms.Position = 0;

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var signatureEntry = zip.Entries.FirstOrDefault(e => e.FullName == "AppxSignature.p7x");
        Assert.NotNull(signatureEntry);

        using var sigStream = signatureEntry.Open();
        using var sigMs = new MemoryStream();
        await sigStream.CopyToAsync(sigMs);
        var sigBytes = sigMs.ToArray();

        // Verify PKCX magic
        Assert.True(sigBytes.Length > 4);
        Assert.Equal(0x50, sigBytes[0]);
        Assert.Equal(0x4B, sigBytes[1]);
        Assert.Equal(0x43, sigBytes[2]);
        Assert.Equal(0x58, sigBytes[3]);
    }

    [Fact]
    public async Task Package_with_icon_generates_visual_assets()
    {
        var fs = new FileSystem();
        var dirInfo = fs.DirectoryInfo.New("TestFiles/MinimalNoMetadata/Contents");
        var container = new DirectoryContainer(dirInfo);

        using var icon = new Image<Rgba32>(256, 256);
        using var iconMs = new MemoryStream();
        icon.SaveAsPng(iconMs);
        var iconBytes = iconMs.ToArray();

        var metadata = new AppManifestMetadata
        {
            Publisher = "CN=TestAssets",
            Executable = "HelloWorld.exe",
        };

        var packager = new MsixPackager();
        var result = await packager.Pack(
            container,
            Maybe.From(metadata),
            sourceIcon: Maybe.From(iconBytes),
            logger: Log.Logger);
        Assert.True(result.IsSuccess);

        using var ms = new MemoryStream();
        await result.Value.WriteTo(ms);
        ms.Position = 0;

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var entryNames = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("Assets/StoreLogo.png", entryNames);
        Assert.Contains("Assets/Square44x44Logo.png", entryNames);
        Assert.Contains("Assets/Square150x150Logo.png", entryNames);
        Assert.Contains("Assets/Wide310x150Logo.png", entryNames);
        Assert.Contains("Assets/SplashScreen.png", entryNames);
        Assert.Contains("AppxManifest.xml", entryNames);
    }

    private static void AssertAssetSize(IReadOnlyDictionary<string, byte[]> assets, string path, int expectedWidth, int expectedHeight)
    {
        Assert.True(assets.ContainsKey(path), $"Missing asset: {path}");
        using var img = Image.Load(assets[path]);
        Assert.Equal(expectedWidth, img.Width);
        Assert.Equal(expectedHeight, img.Height);
    }
}

using System.IO.Compression;
using System.Text.Json;
using CSharpFunctionalExtensions;

namespace DotnetPackaging.Exe;

public static class SimpleExePacker
{
    private const string BrandingLogoEntry = "Branding/logo.png";

    public static async Task<Result> Build(
        string stubPath,
        string publishDir,
        InstallerMetadata metadata,
        Maybe<byte[]> logoBytes,
        string outputPath)
    {
        var tempRoot = string.Empty;
        try
        {
            if (!File.Exists(stubPath))
            {
                return Result.Failure($"Stub not found: {stubPath}");
            }

            if (!Directory.Exists(publishDir))
            {
                return Result.Failure($"Publish directory not found: {publishDir}");
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return Result.Failure("Output directory cannot be determined.");
            }

            Directory.CreateDirectory(outputDirectory);

            tempRoot = Path.Combine(Path.GetTempPath(), "dp-exe-" + Guid.NewGuid());
            Directory.CreateDirectory(tempRoot);

            var uninstallerPayloadRoot = Path.Combine(tempRoot, "uninstaller_payload");
            Directory.CreateDirectory(uninstallerPayloadRoot);
            await WriteMetadata(uninstallerPayloadRoot, metadata);
            await WriteSupportStub(uninstallerPayloadRoot, stubPath);

            var uninstallerPayloadZip = Path.Combine(outputDirectory, "uninstaller_payload.zip");
            CreatePayloadZip(uninstallerPayloadRoot, uninstallerPayloadZip);

            var uninstallerOutput = Path.Combine(outputDirectory, "Uninstaller.exe");
            PayloadAppender.AppendPayload(stubPath, uninstallerPayloadZip, uninstallerOutput);

            var installerPayloadRoot = Path.Combine(tempRoot, "installer_payload");
            Directory.CreateDirectory(installerPayloadRoot);
            await WriteMetadata(installerPayloadRoot, metadata);
            CopyDirectory(publishDir, Path.Combine(installerPayloadRoot, "Content"));
            CopySupportBinary(uninstallerOutput, Path.Combine(installerPayloadRoot, "Support"));
            await WriteLogo(installerPayloadRoot, logoBytes);

            var installerPayloadZip = Path.Combine(outputDirectory, "installer_payload.zip");
            CreatePayloadZip(installerPayloadRoot, installerPayloadZip);

            PayloadAppender.AppendPayload(stubPath, installerPayloadZip, outputPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
        finally
        {
            TryDeleteTempDirectories();
        }

        void TryDeleteTempDirectories()
        {
            if (string.IsNullOrWhiteSpace(tempRoot))
            {
                return;
            }

            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private static void CreatePayloadZip(string sourceDirectory, string destinationZip)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationZip)!);
        if (File.Exists(destinationZip))
        {
            File.Delete(destinationZip);
        }

        ZipFile.CreateFromDirectory(sourceDirectory, destinationZip, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    private static async Task WriteMetadata(string destinationDirectory, InstallerMetadata meta)
    {
        Directory.CreateDirectory(destinationDirectory);
        var metadataPath = Path.Combine(destinationDirectory, "metadata.json");
        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, meta, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private static async Task WriteLogo(string payloadRoot, Maybe<byte[]> logoBytes)
    {
        await logoBytes.Match(
            async bytes =>
            {
                var brandingDir = Path.Combine(payloadRoot, "Branding");
                Directory.CreateDirectory(brandingDir);
                var logoPath = Path.Combine(brandingDir, Path.GetFileName(BrandingLogoEntry));
                await File.WriteAllBytesAsync(logoPath, bytes);
            },
            () => Task.CompletedTask);
    }

    private static async Task WriteSupportStub(string payloadRoot, string stubPath)
    {
        var supportDir = Path.Combine(payloadRoot, "Support");
        Directory.CreateDirectory(supportDir);
        await using var input = File.OpenRead(stubPath);
        await using var output = File.Create(Path.Combine(supportDir, "Uninstaller.exe"));
        await input.CopyToAsync(output);
    }

    private static void CopySupportBinary(string uninstallerPath, string supportRoot)
    {
        Directory.CreateDirectory(supportRoot);
        var destination = Path.Combine(supportRoot, "Uninstaller.exe");
        File.Copy(uninstallerPath, destination, overwrite: true);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}

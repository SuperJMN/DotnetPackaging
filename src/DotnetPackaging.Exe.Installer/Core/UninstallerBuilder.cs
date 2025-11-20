using System;
using System.IO;
using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class UninstallerBuilder
{
    public static Result<string> CreatePayloadlessUninstaller(string installerPath, string destinationPath)
    {
        try
        {
            if (!File.Exists(installerPath))
            {
                return Result.Failure<string>($"Installer path not found: {installerPath}");
            }

            File.Copy(installerPath, destinationPath, overwrite: true);

            var payloadStart = PayloadExtractor.GetAppendedPayloadStart(destinationPath);
            if (payloadStart.HasValue)
            {
                using var stream = File.Open(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                stream.SetLength(payloadStart.Value);
                Log.Information("Embedded payload stripped from uninstaller at {Path}", destinationPath);
            }
            else
            {
                Log.Information("No appended payload detected in installer. Uninstaller kept intact at {Path}", destinationPath);
            }

            return Result.Success(destinationPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create payloadless uninstaller");
            return Result.Failure<string>($"Failed to create payloadless uninstaller: {ex.Message}");
        }
    }
}

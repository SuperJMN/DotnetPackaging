using CSharpFunctionalExtensions;
using Serilog;

namespace DotnetPackaging.Exe.Installer.Core;

internal static class UninstallerBuilder
{
    public static Result<string> CreateSlimCopy(string installerPath, string uninstallerPath)
    {
        return Result.Try(() =>
        {
            var destinationDirectory = Path.GetDirectoryName(uninstallerPath);
            if (destinationDirectory is null)
            {
                throw new InvalidOperationException($"Cannot determine directory for '{uninstallerPath}'.");
            }

            Directory.CreateDirectory(destinationDirectory);

            var payloadStart = PayloadExtractor.GetAppendedPayloadStart(installerPath);
            if (payloadStart.HasValue)
            {
                CopyWithoutPayload(installerPath, uninstallerPath, payloadStart.Value);
                Log.Information("Uninstaller written without embedded payload at {Path}", uninstallerPath);
                return uninstallerPath;
            }

            File.Copy(installerPath, uninstallerPath, overwrite: true);
            Log.Information("Embedded payload not found, copied installer to {Path}", uninstallerPath);
            return uninstallerPath;
        }, ex => $"Failed to create uninstaller: {ex.Message}");
    }

    private static void CopyWithoutPayload(string sourcePath, string destinationPath, long bytesToCopy)
    {
        using var source = File.OpenRead(sourcePath);
        using var destination = File.Create(destinationPath);

        var buffer = new byte[81920];
        var remaining = bytesToCopy;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = source.Read(buffer, 0, toRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of file while copying installer payload.");
            }

            destination.Write(buffer, 0, read);
            remaining -= read;
        }
    }
}

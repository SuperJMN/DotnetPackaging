using Zafiro.DivineBytes;

namespace DotnetPackaging.Exe;

public static class BuildScript
{
    public static async Task Build()
    {
        var stub = ByteSource.FromAsyncStreamFactory(() => Task.FromResult<Stream>(File.OpenRead("build/Stub.exe")));
        var installerPayload = ByteSource.FromAsyncStreamFactory(() => Task.FromResult<Stream>(File.OpenRead("build/installer_payload.zip")));
        var uninstallerPayload = ByteSource.FromAsyncStreamFactory(() => Task.FromResult<Stream>(File.OpenRead("build/uninstaller_payload.zip")));

        Directory.CreateDirectory("artifacts");

        await Persist("artifacts/Uninstaller.exe", PayloadAppender.AppendPayload(stub, uninstallerPayload));
        await Persist("artifacts/Installer.exe", PayloadAppender.AppendPayload(stub, installerPayload));
    }

    private static async Task Persist(string path, IByteSource source)
    {
        await using var input = source.ToStreamSeekable();
        await using var output = File.Create(path);
        await input.CopyToAsync(output);
    }
}

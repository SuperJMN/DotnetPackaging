namespace DotnetPackaging.Exe;

public static class BuildScript
{
    public static void Build()
    {
        var stub = "build/Stub.exe"; // firmado
        var installerPayload = "build/installer_payload.zip";
        var uninstallerPayload = "build/uninstaller_payload.zip";

        PayloadAppender.AppendPayload(stub, uninstallerPayload, "artifacts/Uninstaller.exe");
        PayloadAppender.AppendPayload(stub, installerPayload, "artifacts/Installer.exe");
    }
}

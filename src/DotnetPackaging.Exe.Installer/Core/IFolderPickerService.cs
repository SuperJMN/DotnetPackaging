namespace DotnetPackaging.Exe.Installer.Core;

public interface IFolderPickerService
{
    Task<string?> PickFolder();
}

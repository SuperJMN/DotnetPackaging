namespace DotnetPackaging.Exe.Installer;

public interface IFolderPickerService
{
    Task<string?> PickFolder();
}

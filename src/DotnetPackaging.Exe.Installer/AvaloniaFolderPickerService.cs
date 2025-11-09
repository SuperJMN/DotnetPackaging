using System.Linq;
using Avalonia.Platform.Storage;

namespace DotnetPackaging.Exe.Installer;

public sealed class AvaloniaFolderPickerService(IStorageProvider storageProvider) : IFolderPickerService
{
    public async Task<string?> PickFolder()
    {
        var results = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        var first = results?.FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        // Prefer LocalPath for file:// URIs; fallback to string otherwise
        return first.Path.IsAbsoluteUri ? first.Path.LocalPath : first.Path.ToString();
    }
}

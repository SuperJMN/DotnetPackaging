using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Metadata;

internal class MetadataResolver : IMetadataResolver
{
    private readonly Executable executableFile;

    public MetadataResolver(Executable executableFile)
    {
        this.executableFile = executableFile;
    }

    public Zafiro.DivineBytes.Unix.Metadata ResolveDirectory(INamedContainer dir)
    {
        // Standard directory permissions: 755 (rwxr-xr-x)
        // Owner: read, write, execute
        // Group and Others: read, execute
        var directoryPermissions = new UnixPermissions(
            Permission.OwnerRead | Permission.OwnerWrite | Permission.OwnerExec |
            Permission.GroupRead | Permission.GroupExec |
            Permission.OtherRead | Permission.OtherExec
        );
        
        return new Zafiro.DivineBytes.Unix.Metadata(directoryPermissions, 1000);
    }

    public Zafiro.DivineBytes.Unix.Metadata ResolveFile(INamedByteSource file)
    {
        // Check if this is the main executable file
        var isExecutable = executableFile.Resource.Name.Equals(file.Name, StringComparison.OrdinalIgnoreCase);
        // Check if this is the AppRun file, which is also executable
        var isAppRun = file.Name.Equals("AppRun", StringComparison.OrdinalIgnoreCase);
        
        UnixPermissions filePermissions;
        
        if (isExecutable || isAppRun)
        {
            // Executable file permissions: 755 (rwxr-xr-x)
            // Owner: read, write, execute
            // Group and Others: read, execute
            filePermissions = new UnixPermissions(
                Permission.OwnerRead | Permission.OwnerWrite | Permission.OwnerExec |
                Permission.GroupRead | Permission.GroupExec |
                Permission.OtherRead | Permission.OtherExec
            );
        }
        else
        {
            // Regular file permissions: 644 (rw-r--r--)
            // Owner: read, write
            // Group and Others: read only
            filePermissions = new UnixPermissions(
                Permission.OwnerRead | Permission.OwnerWrite |
                Permission.GroupRead |
                Permission.OtherRead
            );
        }
        
        return new Zafiro.DivineBytes.Unix.Metadata(filePermissions, 1000);
    }
}
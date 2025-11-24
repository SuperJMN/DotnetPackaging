using DotnetPackaging.Deb.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public record TarDirectoryProperties : UnixFileProperties
{
    public static TarDirectoryProperties From(UnixFileProperties properties)
    {
        return new TarDirectoryProperties()
        {
            FileMode = properties.FileMode,
            GroupId = properties.GroupId,
            GroupName = properties.GroupName,
            LastModification = properties.LastModification,
            OwnerId = properties.OwnerId,
            OwnerUsername = properties.OwnerUsername,
        };
    }
}
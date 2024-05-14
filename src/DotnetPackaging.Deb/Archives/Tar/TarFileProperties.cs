using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;


public record TarFileProperties : UnixFileProperties
{
    public static TarFileProperties From(UnixFileProperties unixFileProperties)
    {
        return new TarFileProperties()
        {
            FileMode = unixFileProperties.FileMode,
            GroupId = unixFileProperties.GroupId,
            GroupName = unixFileProperties.GroupName,
            LastModification = unixFileProperties.LastModification,
            OwnerId = unixFileProperties.OwnerId,
            OwnerUsername = unixFileProperties.OwnerUsername,
        };
    }
}
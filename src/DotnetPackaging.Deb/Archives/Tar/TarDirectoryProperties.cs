namespace DotnetPackaging.Deb.Archives.Tar;

public record TarDirectoryProperties : TarEntryProperties
{
    public static TarDirectoryProperties From(TarEntryProperties properties)
    {
        return new TarDirectoryProperties
        {
            Permissions = properties.Permissions,
            GroupId = properties.GroupId,
            GroupName = properties.GroupName,
            LastModification = properties.LastModification,
            OwnerId = properties.OwnerId,
            OwnerUsername = properties.OwnerUsername,
        };
    }
}

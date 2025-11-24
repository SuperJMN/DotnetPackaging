namespace DotnetPackaging.Deb.Archives.Tar;

public record TarFileProperties : TarEntryProperties
{
    public static TarFileProperties From(TarEntryProperties properties)
    {
        return new TarFileProperties
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

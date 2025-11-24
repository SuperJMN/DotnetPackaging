namespace DotnetPackaging.Deb.Archives.Tar;

public static class Misc
{
    public static TarFileProperties RegularFileProperties() => new()
    {
        Mode = UnixPermissions.Parse("644"),
        GroupId = 1000,
        OwnerId = 1000,
        GroupName = "root",
        OwnerUsername = "root",
        LastModification = DateTimeOffset.Now
    };

    public static TarFileProperties ExecutableFileProperties() => RegularFileProperties() with { Mode = UnixPermissions.Parse("755") };
}

using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class Misc
{
    public static TarFileProperties RegularFileProperties() => new()
    {
        Permissions = UnixPermissionHelper.FromOctal("644"),
        GroupId = 1000,
        OwnerId = 1000,
        GroupName = "root",
        OwnerUsername = "root",
        LastModification = DateTimeOffset.Now
    };

    public static TarFileProperties ExecutableFileProperties() => RegularFileProperties() with
    {
        Permissions = UnixPermissionHelper.FromOctal("755")
    };
}

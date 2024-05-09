using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public class Misc
{
    public static TarFileProperties RegularFileProperties() => new()
    {
        FileMode = UnixFilePermissionsMixin.ConvertToUnixFileMode("644"),
        GroupId = 1000,
        OwnerId = 1000,
        GroupName = "root",
        OwnerUsername = "root",
        LastModification = DateTimeOffset.Now
    };

    public static TarFileProperties ExecutableFileProperties() => RegularFileProperties() with { FileMode = UnixFilePermissionsMixin.ConvertToUnixFileMode("755") };
}
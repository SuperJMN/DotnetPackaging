using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

public class Misc
{
    public static TarFileProperties RegularFileProperties() => new()
    {
        FileMode = "644".ToFileMode(),
        GroupId = 1000,
        OwnerId = 1000,
        GroupName = "root",
        OwnerUsername = "root",
        LastModification = DateTimeOffset.Now
    };

    public static TarFileProperties ExecutableFileProperties() => RegularFileProperties() with { FileMode = "755".ToFileMode() };
}
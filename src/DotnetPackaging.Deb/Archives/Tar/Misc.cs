using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Unix;

namespace DotnetPackaging.Deb.Archives.Tar;

    public class Misc
    {
        public static TarFileProperties RegularFileProperties() => new()
        {
            FileMode = "644".ToFileMode(),
            GroupId = Maybe<int>.From(1000),
            OwnerId = Maybe<int>.From(1000),
            GroupName = Maybe<string>.From("root"),
            OwnerUsername = Maybe<string>.From("root"),
            LastModification = DateTimeOffset.Now
        };

    public static TarFileProperties ExecutableFileProperties() => RegularFileProperties() with { FileMode = "755".ToFileMode() };
}
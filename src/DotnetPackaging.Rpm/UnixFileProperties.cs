using System.Globalization;

namespace DotnetPackaging.Rpm;

internal record UnixFileProperties
{
    public required int FileMode { get; init; }
    public string OwnerUsername { get; init; } = "root";
    public string GroupName { get; init; } = "root";

    public static UnixFileProperties RegularFileProperties() => new()
    {
        FileMode = ParseMode("644"),
        OwnerUsername = "root",
        GroupName = "root"
    };

    public static UnixFileProperties ExecutableFileProperties() => RegularFileProperties() with
    {
        FileMode = ParseMode("755")
    };

    public static UnixFileProperties RegularDirectoryProperties() => ExecutableFileProperties();

    public string ToFileModeString() => Convert.ToString(FileMode, 8);

    public static int ParseMode(string octal) => Convert.ToInt32(octal, 8);
}

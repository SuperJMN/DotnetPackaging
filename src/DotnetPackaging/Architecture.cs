namespace DotnetPackaging;

public class Architecture
{
    public string FriendlyName { get; }
    public string Name { get; }
    public string PackagePrefix { get; }

    public static Architecture X86 = new("X86", "i386", "i386");
    public static Architecture X64 = new("X64", "amd64", "x86_64");
    public static Architecture Arm64 = new("ARM64", "arm64", "aarch64"); 
    public static Architecture Arm32 = new("ARM32", "armhf", "armhf");
    public static Architecture All = new("All", "all", "all");

    private Architecture(string friendlyName, string name, string packagePrefix)
    {
        FriendlyName = friendlyName;
        Name = name;
        PackagePrefix = packagePrefix;
    }

    public override string ToString() => FriendlyName;
}
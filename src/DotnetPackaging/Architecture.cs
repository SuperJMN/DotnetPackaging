namespace DotnetPackaging;

public class Architecture
{
    public string Name { get; }
    public string PackagePrefix { get; }

    public static Architecture X86 = new("i386", "i386");
    public static Architecture X64 = new("amd64", "x86_64");
    public static Architecture Arm64 = new("arm64", "aarch64"); 
    public static Architecture Arm32 = new("armhf", "armhf");
    public static Architecture All = new("all", "all");

    private Architecture(string name, string packagePrefix)
    {
        Name = name;
        PackagePrefix = packagePrefix;
    }

    public override string ToString() => Name;
}
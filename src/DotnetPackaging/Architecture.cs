namespace DotnetPackaging;

public class Architecture
{
    public string Name { get; }
    public string PackagePrefix { get; }
    public static Architecture All = new("all", "all");
    public static Architecture X64 = new("amd64", "x86_64");

    private Architecture(string name, string packagePrefix)
    {
        Name = name;
        PackagePrefix = packagePrefix;
    }
}
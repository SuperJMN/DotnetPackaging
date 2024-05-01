namespace DotnetPackaging;

public class FileName
{
    public static string FromMetadata(PackageMetadata metadata)
    {
        return $"{metadata.Package}-{metadata.Version.GetValueOrDefault("1.0.0")}-{metadata.Architecture.PackagePrefix}";
    }
}
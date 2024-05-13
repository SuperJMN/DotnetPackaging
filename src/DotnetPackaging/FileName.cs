namespace DotnetPackaging;

public class FileName
{
    public static string FromMetadata(PackageMetadata metadata)
    {
        return $"{metadata.Package}-{metadata.Version}-{metadata.Architecture.PackagePrefix}";
    }
}
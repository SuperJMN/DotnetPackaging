namespace Archiver;

public class Metadata
{
    public Metadata(string applicationName, string packageName, string maintainer)
    {
        ApplicationName = applicationName;
        PackageName = packageName;
        Maintainer = maintainer;
    }

    public string Maintainer { get; }

    public string PackageName { get; }

    public string ApplicationName { get; }
    public string Architecture { get; set; }
    public string Homepage { get; set; }
    public string License { get; set; }
}
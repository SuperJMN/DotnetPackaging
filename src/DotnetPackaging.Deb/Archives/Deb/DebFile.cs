using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Deb;

public record DebFile
{
    public ControlMetadata ControlMetadata { get; }
    public FileEntry[] Data { get; }

    public DebFile(ControlMetadata controlMetadata, params FileEntry[] data)
    {
        ControlMetadata = controlMetadata;
        Data = data;
    }
}

public record FileEntry
{
    public RootedFile File { get; }
    public UnixFileProperties UnixFileProperties { get; }

    public FileEntry(RootedFile file, UnixFileProperties unixFileProperties)
    {
        File = file;
        UnixFileProperties = unixFileProperties;
    }
}

public record ControlMetadata
{
    public string Package { get; set; }
    public string Version { get; set; }
    public string Section { get; set; }
    public string Priority { get; set; }
    public string Architecture { get; set; }
    public string Maintainer { get; set; }
    public string Description { get; set; }
    public string Homepage { get; set; }
    public string License { get; set; }
    public string Recommends { get; set; }
    public string VcsGit { get; set; }
    public string VcsBrowser { get; set; }
    public string InstalledSize { get; set; }
    public DateTimeOffset ModificationTime { get; set; }
}


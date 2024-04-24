using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Deb;

public class DebFile
{
    public DebFile()
    {
        
    }

    public DebFile(ControlMetadata controlMetadata)
    {
        
    }

    public DebFile(ControlMetadata controlMetadata, params FileEntry[] fileEntry)
    {
        throw new NotImplementedException();
    }
}

public class FileEntry
{
    public FileEntry(RootedFile file, UnixFileProperties unixFileProperties)
    {
        throw new NotImplementedException();
    }
}

public class ControlMetadata
{
    public string Package { get; set; }
    public string Version { get; set; }
    public string Section { get; set; }
    public string Priority { get; set; }
    public string Architecture { get; set; }
    public string Maintainer { get; set; }
    public string Description { get; set; }
}


namespace DotnetPackaging.Hfs.Files;

/// <summary>
/// Base class for HFS+ file system entries.
/// </summary>
public abstract record HfsEntry
{
    public required string Name { get; init; }
    public DateTime CreateDate { get; init; } = DateTime.UtcNow;
    public DateTime ModifyDate { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a file in the HFS+ volume.
/// </summary>
public sealed record HfsFile : HfsEntry
{
    public required IByteSource Content { get; init; }
    public long Size { get; init; }
}

/// <summary>
/// Represents a directory in the HFS+ volume.
/// </summary>
public sealed record HfsDirectory : HfsEntry
{
    public List<HfsEntry> Children { get; init; } = new();

    public HfsDirectory AddFile(string name, IByteSource content, long size)
    {
        Children.Add(new HfsFile { Name = name, Content = content, Size = size });
        return this;
    }

    public HfsDirectory AddFile(string name, byte[] content)
    {
        Children.Add(new HfsFile 
        { 
            Name = name, 
            Content = ByteSource.FromBytes(content), 
            Size = content.Length 
        });
        return this;
    }

    public HfsDirectory AddDirectory(string name)
    {
        var dir = new HfsDirectory { Name = name };
        Children.Add(dir);
        return dir;
    }

    public HfsDirectory AddSymlink(string name, string target)
    {
        Children.Add(new HfsSymlink { Name = name, Target = target });
        return this;
    }
}

/// <summary>
/// Represents a symbolic link in the HFS+ volume.
/// </summary>
public sealed record HfsSymlink : HfsEntry
{
    public required string Target { get; init; }
}

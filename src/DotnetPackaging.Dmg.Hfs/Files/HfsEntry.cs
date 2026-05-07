namespace DotnetPackaging.Dmg.Hfs.Files;

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
    public ushort FileMode { get; init; } = HfsFileModes.Regular0644;
}

/// <summary>
/// Represents a directory in the HFS+ volume.
/// </summary>
public sealed record HfsDirectory : HfsEntry
{
    public List<HfsEntry> Children { get; init; } = new();

    public HfsDirectory AddFile(string name, IByteSource content, long size)
    {
        return AddFile(name, content, size, HfsFileModes.Regular0644);
    }

    public HfsDirectory AddFile(string name, IByteSource content, long size, ushort fileMode)
    {
        Children.Add(new HfsFile { Name = name, Content = content, Size = size, FileMode = fileMode });
        return this;
    }

    public HfsDirectory AddFile(string name, byte[] content)
    {
        return AddFile(name, content, HfsFileModes.Regular0644);
    }

    public HfsDirectory AddFile(string name, byte[] content, ushort fileMode)
    {
        Children.Add(new HfsFile 
        { 
            Name = name, 
            Content = ByteSource.FromBytes(content), 
            Size = content.Length,
            FileMode = fileMode
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

public static class HfsFileModes
{
    private const ushort RegularFileType = 0x8000;

    public const ushort Regular0644 = RegularFileType | 0x01A4;
    public const ushort Regular0755 = RegularFileType | 0x01ED;
    public const ushort Directory0755 = 0x4000 | 0x01ED;
    public const ushort Symlink0755 = 0xA000 | 0x01ED;

    public static ushort Regular(ushort permissions)
    {
        return (ushort)(RegularFileType | (permissions & 0x01FF));
    }
}

using DotnetPackaging.Dmg.Hfs.Files;

namespace DotnetPackaging.Dmg.Hfs.Builder;

/// <summary>
/// Builder for creating HFS+ volumes.
/// </summary>
public sealed class HfsVolumeBuilder
{
    private string volumeName = "Untitled";
    private uint blockSize = 4096;
    private readonly HfsDirectory root = new() { Name = string.Empty };

    private HfsVolumeBuilder() { }

    /// <summary>
    /// Creates a new volume builder.
    /// </summary>
    public static HfsVolumeBuilder Create(string volumeName)
    {
        return new HfsVolumeBuilder { volumeName = volumeName };
    }

    /// <summary>
    /// Sets the allocation block size.
    /// </summary>
    public HfsVolumeBuilder WithBlockSize(uint blockSize)
    {
        this.blockSize = blockSize;
        return this;
    }

    /// <summary>
    /// Gets the root directory for adding content.
    /// </summary>
    public HfsDirectory Root => root;

    /// <summary>
    /// Adds a file to the root directory.
    /// </summary>
    public HfsVolumeBuilder AddFile(string name, IByteSource content, long size)
    {
        root.AddFile(name, content, size);
        return this;
    }

    /// <summary>
    /// Adds a file with byte array content to the root directory.
    /// </summary>
    public HfsVolumeBuilder AddFile(string name, byte[] content)
    {
        root.AddFile(name, content);
        return this;
    }

    /// <summary>
    /// Adds a directory to the root.
    /// </summary>
    public HfsDirectory AddDirectory(string name)
    {
        return root.AddDirectory(name);
    }

    /// <summary>
    /// Adds a symlink to the root directory.
    /// </summary>
    public HfsVolumeBuilder AddSymlink(string name, string target)
    {
        root.AddSymlink(name, target);
        return this;
    }

    /// <summary>
    /// Builds the HFS+ volume.
    /// </summary>
    public HfsVolume Build()
    {
        return new HfsVolume(volumeName, blockSize, root);
    }
}

/// <summary>
/// Represents a built HFS+ volume ready for serialization.
/// </summary>
public sealed class HfsVolume
{
    public string VolumeName { get; }
    public uint BlockSize { get; }
    public HfsDirectory Root { get; }

    internal HfsVolume(string volumeName, uint blockSize, HfsDirectory root)
    {
        VolumeName = volumeName;
        BlockSize = blockSize;
        Root = root;
    }

    /// <summary>
    /// Counts all entries (files + folders) in the volume.
    /// </summary>
    public (uint files, uint folders) CountEntries()
    {
        uint files = 0;
        uint folders = 0;
        CountRecursive(Root, ref files, ref folders);
        return (files, folders);
    }

    private static void CountRecursive(HfsDirectory dir, ref uint files, ref uint folders)
    {
        foreach (var entry in dir.Children)
        {
            switch (entry)
            {
                case HfsDirectory subDir:
                    folders++;
                    CountRecursive(subDir, ref files, ref folders);
                    break;
                case HfsFile:
                case HfsSymlink:
                    files++;
                    break;
            }
        }
    }

    /// <summary>
    /// Writes the volume to an IByteSource.
    /// </summary>
    public IByteSource ToByteSource()
    {
        return HfsVolumeWriter.Write(this);
    }
}

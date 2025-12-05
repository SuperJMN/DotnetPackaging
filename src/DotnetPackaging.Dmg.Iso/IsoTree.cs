using Zafiro.DivineBytes;

namespace DotnetPackaging.Formats.Dmg.Iso
{
    public abstract class IsoNode
    {
        public string Name { get; set; }
        public IsoDirectory? Parent { get; set; }
        public int SectorLocation { get; set; }
        public int DataLength { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public int Mode { get; set; } = 0x81ED; // Default file mode (0100755)
        public int Uid { get; set; } = 0;
        public int Gid { get; set; } = 0;

        public IsoNode(string name)
        {
            Name = name;
        }
    }

    public class IsoFile : IsoNode
    {
        public Func<IByteSource> ContentSource { get; set; } = () => ByteSource.FromBytes(Array.Empty<byte>());
        public string? SourcePath { get; set; } // For reference

        public IsoFile(string name) : base(name)
        {
            Mode = 0x81ED; // Regular file, 755
        }
    }

    public class IsoDirectory : IsoNode
    {
        public List<IsoNode> Children { get; } = new List<IsoNode>();

        public IsoDirectory(string name) : base(name)
        {
            Mode = 0x41ED; // Directory, 755
        }

        public void AddChild(IsoNode node)
        {
            node.Parent = this;
            Children.Add(node);
        }

        public IsoDirectory AddDirectory(string name)
        {
            var dir = new IsoDirectory(name);
            AddChild(dir);
            return dir;
        }
    }

    public class IsoSymlink : IsoNode
    {
        public string TargetPath { get; set; }

        public IsoSymlink(string name, string targetPath) : base(name)
        {
            TargetPath = targetPath;
            Mode = 0xA1ED; // Symlink (0120755)
        }
    }
}

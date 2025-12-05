using System.Text;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Formats.Dmg.Iso
{
    public class IsoBuilder
    {
        private IsoDirectory _root;
        private string _volumeIdentifier = "DOTNET_DMG";
        private int _totalSectors;

        public IsoBuilder(string volumeIdentifier)
        {
            _volumeIdentifier = volumeIdentifier;
            _root = new IsoDirectory("");
        }

        public IsoDirectory Root => _root;

        public void Build(Stream output)
        {
            // 1. Layout calculation
            int currentSector = 16; // Start after System Area

            // PVD + Terminator
            int pvdSector = currentSector++;
            int terminatorSector = currentSector++;

            // Path Tables
            var allDirs = FlattenDirectories(_root);

            // Calculate Path Table size
            int pathTableBytes = 0;
            foreach (var dir in allDirs)
            {
                int nameLen = dir.Name.Length;
                if (dir == _root) nameLen = 1;

                int entrySize = 8 + nameLen;
                if (entrySize % 2 != 0) entrySize++;
                pathTableBytes += entrySize;
            }

            int pathTableSectors = (pathTableBytes + IsoConstants.SectorSize - 1) / IsoConstants.SectorSize;
            int typeLPathTableSector = currentSector;
            currentSector += pathTableSectors;
            int typeMPathTableSector = currentSector;
            currentSector += pathTableSectors;

            // Directories
            foreach (var dir in allDirs)
            {
                dir.SectorLocation = currentSector;
                int dirSize = CalculateDirectorySize(dir);
                dir.DataLength = dirSize;
                int dirSectors = (dirSize + IsoConstants.SectorSize - 1) / IsoConstants.SectorSize;
                currentSector += dirSectors;
            }

            // Files
            var allFiles = FlattenFiles(_root);
            foreach (var node in allFiles)
            {
                node.SectorLocation = currentSector;
                if (node is IsoFile file)
                {
                    using (var s = file.ContentSource().ToStreamSeekable())
                    {
                        file.DataLength = (int)s.Length;
                    }
                    int fileSectors = (file.DataLength + IsoConstants.SectorSize - 1) / IsoConstants.SectorSize;
                    currentSector += fileSectors;
                }
                else if (node is IsoSymlink)
                {
                    node.DataLength = 0;
                }
            }

            _totalSectors = currentSector;

            // 2. Writing

            // System Area (16 sectors of 0)
            WriteZeros(output, 16 * IsoConstants.SectorSize);

            // PVD
            WritePVD(output, pvdSector, typeLPathTableSector, typeMPathTableSector, pathTableBytes, _root);

            // Terminator
            WriteTerminator(output);

            // Path Tables
            WritePathTable(output, allDirs, true); // Type L
            WritePathTable(output, allDirs, false); // Type M

            // Directories
            foreach (var dir in allDirs)
            {
                WriteDirectory(output, dir, allDirs);
            }

            // Files
            foreach (var node in allFiles)
            {
                if (node is IsoFile file)
                {
                    using (var s = file.ContentSource().ToStream())
                    {
                        s.CopyTo(output);
                    }
                    int padding = IsoConstants.SectorSize - (int)(file.DataLength % IsoConstants.SectorSize);
                    if (padding < IsoConstants.SectorSize)
                    {
                        WriteZeros(output, padding);
                    }
                }
            }
        }

        private void WritePVD(Stream s, int pvdSector, int typeL, int typeM, int pathTableSize, IsoDirectory root)
        {
            byte[] buffer = new byte[IsoConstants.SectorSize];
            using (var ms = new MemoryStream(buffer))
            using (var w = new BinaryWriter(ms))
            {
                w.Write((byte)1); // Type
                w.Write(Encoding.ASCII.GetBytes("CD001"));
                w.Write((byte)1); // Version
                w.Write((byte)0); // Unused
                w.Write(IsoUtilities.StringToBytes("LINUX", 32)); // System ID
                w.Write(IsoUtilities.StringToBytes(_volumeIdentifier, 32)); // Volume ID
                w.Write(new byte[8]); // Unused
                w.Write(IsoUtilities.ToBothEndian(_totalSectors)); // Volume Space Size
                w.Write(new byte[32]); // Unused
                w.Write(IsoUtilities.ToBothEndian((short)1)); // Volume Set Size
                w.Write(IsoUtilities.ToBothEndian((short)1)); // Volume Sequence Number
                w.Write(IsoUtilities.ToBothEndian((short)2048)); // Logical Block Size
                w.Write(IsoUtilities.ToBothEndian(pathTableSize)); // Path Table Size
                w.Write(typeL); // Type L Location (LE)
                w.Write(0); // Optional Type L
                w.Write(SwapEndian(typeM)); // Type M Location (BE)
                w.Write(0); // Optional Type M

                // Root Directory Record
                byte[] rootRecord = GenerateDirectoryRecord(root, ".", root);
                w.Write(rootRecord);

                // Pad remaining root record space? No, root record is variable length but PVD has 34 bytes reserved?
                // No, PVD definition says "Directory Record for Root Directory". It's variable length.
                // But PVD structure is fixed 2048.
                // We need to write the root record, then fill the rest.

                // Wait, PVD structure has fixed offsets.
                // Root Directory Record starts at byte 156.
                // We need to ensure we are at 156.
                // The previous writes:
                // 1+5+1+1+32+32+8+8+32+4+4+4+8+4+4+4+4 = 156. Correct.

                // Volume Set ID
                ms.Seek(156 + rootRecord.Length, SeekOrigin.Begin);
                // Actually PVD has fixed fields after Root Record?
                // No, "Volume Set Identifier" is at byte 190.
                // So Root Record must fit in 34 bytes?
                // "The Directory Record for the Root Directory shall be recorded starting at the 157th byte... The length... shall be 34."
                // Wait, if we add Rock Ridge to Root Record, it will be larger than 34 bytes!
                // Standard ISO 9660 says Root Record in PVD should be basic.
                // Extensions usually go in the actual Directory entry for Root.
                // But Rock Ridge says "The Root Directory Record in the PVD... should contain...".
                // Actually, usually the PVD root record is kept minimal (34 bytes) and the real one in the directory extent has the extensions.
                // Let's keep PVD root record minimal.

                // Reset root record to minimal for PVD
                byte[] minimalRoot = GenerateDirectoryRecord(root, ".", root, minimal: true);
                ms.Seek(156, SeekOrigin.Begin);
                w.Write(minimalRoot);

                ms.Seek(190, SeekOrigin.Begin);
                w.Write(IsoUtilities.StringToBytes("", 128)); // Vol Set ID
                w.Write(IsoUtilities.StringToBytes("DOTNET_DMG", 128)); // Publisher
                w.Write(IsoUtilities.StringToBytes("", 128)); // Data Preparer
                w.Write(IsoUtilities.StringToBytes("", 128)); // Application
                w.Write(IsoUtilities.StringToBytes("", 37)); // Copyright
                w.Write(IsoUtilities.StringToBytes("", 37)); // Abstract
                w.Write(IsoUtilities.StringToBytes("", 37)); // Biblio
                w.Write(IsoUtilities.DateToBytes(root.CreationTime)); // Creation
                w.Write(IsoUtilities.DateToBytes(DateTime.UtcNow)); // Mod
                w.Write(IsoUtilities.DateToBytes(DateTime.MaxValue)); // Exp
                w.Write(IsoUtilities.DateToBytes(DateTime.MinValue)); // Eff
                w.Write((byte)1); // File Structure Version
                w.Write((byte)0); // Unused

                // Application Use (512) + Reserved (653)
            }
            s.Write(buffer, 0, buffer.Length);
        }

        private void WriteTerminator(Stream s)
        {
            byte[] buffer = new byte[IsoConstants.SectorSize];
            buffer[0] = 255;
            buffer[1] = (byte)'C';
            buffer[2] = (byte)'D';
            buffer[3] = (byte)'0';
            buffer[4] = (byte)'0';
            buffer[5] = (byte)'1';
            buffer[6] = 1;
            s.Write(buffer, 0, buffer.Length);
        }

        private void WritePathTable(Stream s, List<IsoDirectory> dirs, bool typeL)
        {
            // Path Table is packed.
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                foreach (var dir in dirs)
                {
                    int nameLen = dir.Name.Length;
                    string name = GetIsoName(dir.Name);
                    if (dir == _root)
                    {
                        nameLen = 1;
                        name = "\0";
                    }

                    w.Write((byte)nameLen);
                    w.Write((byte)0); // Ext Len

                    if (typeL)
                        w.Write(dir.SectorLocation);
                    else
                        w.Write(SwapEndian(dir.SectorLocation));

                    short parentIndex = 1;
                    if (dir != _root)
                    {
                        // Find parent index (1-based) in dirs list
                        parentIndex = (short)(dirs.IndexOf(dir.Parent) + 1);
                    }

                    if (typeL)
                        w.Write(parentIndex);
                    else
                        w.Write(SwapEndian(parentIndex));

                    if (dir == _root)
                        w.Write((byte)0);
                    else
                        w.Write(Encoding.ASCII.GetBytes(name));

                    if (ms.Position % 2 != 0) w.Write((byte)0); // Pad
                }

                // Pad to sector size
                byte[] data = ms.ToArray();
                s.Write(data, 0, data.Length);
                int padding = IsoConstants.SectorSize - (int)(data.Length % IsoConstants.SectorSize);
                if (padding < IsoConstants.SectorSize) WriteZeros(s, padding);
            }
        }

        private void WriteDirectory(Stream s, IsoDirectory dir, List<IsoDirectory> allDirs)
        {
            using (var ms = new MemoryStream())
            {
                // Write . and ..
                ms.Write(GenerateDirectoryRecord(dir, ".", dir));
                ms.Write(GenerateDirectoryRecord(dir.Parent ?? dir, "..", dir));

                foreach (var child in dir.Children)
                {
                    ms.Write(GenerateDirectoryRecord(child, child.Name, dir));
                }

                byte[] data = ms.ToArray();
                s.Write(data, 0, data.Length);
                int padding = IsoConstants.SectorSize - (int)(data.Length % IsoConstants.SectorSize);
                if (padding < IsoConstants.SectorSize) WriteZeros(s, padding);
            }
        }

        private byte[] GenerateDirectoryRecord(IsoNode node, string name, IsoDirectory parent, bool minimal = false)
        {
            // Directory Record
            // 0: Len
            // 1: Ext Attr Len
            // 2: Location (8)
            // 10: Data Len (8)
            // 18: Date (7)
            // 25: Flags (1)
            // 26: Unit Size (1)
            // 27: Gap Size (1)
            // 28: Vol Seq (4)
            // 32: Name Len (1)
            // 33: Name
            // Pad
            // System Use

            string isoName = GetIsoName(name);
            int nameLen = isoName.Length;
            if (name == ".") { isoName = "\0"; nameLen = 1; }
            if (name == "..") { isoName = "\x01"; nameLen = 1; }

            byte[] susp = minimal ? new byte[0] : GenerateSUSP(node, name, parent);

            int recordLen = 33 + nameLen;
            if (recordLen % 2 != 0) recordLen++;
            recordLen += susp.Length;
            if (recordLen % 2 != 0) recordLen++;

            byte[] data = new byte[recordLen];
            data[0] = (byte)recordLen;
            data[1] = 0;

            Array.Copy(IsoUtilities.ToBothEndian(node.SectorLocation), 0, data, 2, 8);
            Array.Copy(IsoUtilities.ToBothEndian(node.DataLength), 0, data, 10, 8);

            // Date 7 bytes: Y M D H M S Offset
            var dt = node.CreationTime;
            data[18] = (byte)(dt.Year - 1900);
            data[19] = (byte)dt.Month;
            data[20] = (byte)dt.Day;
            data[21] = (byte)dt.Hour;
            data[22] = (byte)dt.Minute;
            data[23] = (byte)dt.Second;
            data[24] = 0;

            byte flags = 0;
            if (node is IsoDirectory) flags |= 2;
            data[25] = flags;

            data[26] = 0;
            data[27] = 0;

            Array.Copy(IsoUtilities.ToBothEndian((short)1), 0, data, 28, 4);

            data[32] = (byte)nameLen;
            Array.Copy(Encoding.ASCII.GetBytes(isoName), 0, data, 33, nameLen);

            // Padding after name if needed
            int offset = 33 + nameLen;
            if (offset % 2 != 0) offset++;

            if (susp.Length > 0)
            {
                Array.Copy(susp, 0, data, offset, susp.Length);
            }

            return data;
        }

        // ... (Include FlattenDirectories, FlattenFiles, CalculateDirectorySize, GetIsoName, GenerateSUSP, WriteZeros from previous snippet)

        private List<IsoDirectory> FlattenDirectories(IsoDirectory root)
        {
            var list = new List<IsoDirectory>();
            var queue = new Queue<IsoDirectory>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var d = queue.Dequeue();
                list.Add(d);
                foreach (var c in d.Children.OfType<IsoDirectory>())
                {
                    queue.Enqueue(c);
                }
            }
            return list;
        }

        private List<IsoNode> FlattenFiles(IsoDirectory root)
        {
            var list = new List<IsoNode>();
            var queue = new Queue<IsoDirectory>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var d = queue.Dequeue();
                foreach (var c in d.Children)
                {
                    if (c is IsoDirectory dir)
                    {
                        queue.Enqueue(dir);
                    }
                    else
                    {
                        list.Add(c);
                    }
                }
            }
            return list;
        }

        private int CalculateDirectorySize(IsoDirectory dir)
        {
            int size = 0;
            size += CalculateDirectoryRecordSize(dir, ".", dir);
            size += CalculateDirectoryRecordSize(dir.Parent ?? dir, "..", dir);

            foreach (var child in dir.Children)
            {
                size += CalculateDirectoryRecordSize(child, child.Name, dir);
            }
            return size;
        }

        private int CalculateDirectoryRecordSize(IsoNode node, string name, IsoDirectory parent)
        {
            byte[] susp = GenerateSUSP(node, name, parent);
            int nameLen = name == "." || name == ".." ? 1 : Encoding.ASCII.GetByteCount(GetIsoName(name));

            int len = 33 + nameLen;
            if (len % 2 != 0) len++;
            len += susp.Length;
            if (len % 2 != 0) len++;

            return len;
        }

        private string GetIsoName(string name)
        {
            if (name == ".") return ".";
            if (name == "..") return "..";
            // Strict ISO 9660: UPPERCASE, _, ., 0-9. Max 8.3?
            // We use Rock Ridge, so ISO name can be anything unique.
            // Let's just normalize to valid chars.
            var sb = new StringBuilder();
            foreach (char c in name.ToUpper())
            {
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            return sb.ToString();
        }

        private byte[] GenerateSUSP(IsoNode node, string name, IsoDirectory parent)
        {
            List<byte> susp = new List<byte>();

            if (node is IsoDirectory && name == "." && node == _root)
            {
                susp.AddRange(RockRidge.CreateSP());
            }

            susp.AddRange(RockRidge.CreateRR());
            susp.AddRange(RockRidge.CreatePX(node.Mode, 1, node.Uid, node.Gid));

            if (name != "." && name != "..")
            {
                susp.AddRange(RockRidge.CreateNM(node.Name));
            }

            if (node is IsoSymlink symlink)
            {
                susp.AddRange(RockRidge.CreateSL(symlink.TargetPath, false));
            }

            return susp.ToArray();
        }

        private void WriteZeros(Stream s, int count)
        {
            byte[] buffer = new byte[count];
            s.Write(buffer, 0, count);
        }

        private int SwapEndian(int v)
        {
            return System.Net.IPAddress.HostToNetworkOrder(v);
        }

        private short SwapEndian(short v)
        {
            return (short)System.Net.IPAddress.HostToNetworkOrder(v);
        }
    }
}

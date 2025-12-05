using System.Text;

namespace DotnetPackaging.Formats.Dmg.Iso
{
    public static class RockRidge
    {
        public static byte[] CreateSP()
        {
            // SP - System Use Sharing Protocol Indicator
            // Check bytes: 0xBE, 0xEF
            byte[] data = new byte[7];
            data[0] = (byte)'S';
            data[1] = (byte)'P';
            data[2] = 7; // Length
            data[3] = 1; // Version
            data[4] = 0xBE;
            data[5] = 0xEF;
            data[6] = 0; // Len_skp
            return data;
        }

        public static byte[] CreateER(string id, string desc, string src)
        {
            // ER - Extensions Reference
            byte[] idBytes = Encoding.ASCII.GetBytes(id);
            byte[] descBytes = Encoding.ASCII.GetBytes(desc);
            byte[] srcBytes = Encoding.ASCII.GetBytes(src);

            int len = 8 + idBytes.Length + descBytes.Length + srcBytes.Length;
            byte[] data = new byte[len];

            data[0] = (byte)'E';
            data[1] = (byte)'R';
            data[2] = (byte)len;
            data[3] = 1; // Version
            data[4] = (byte)idBytes.Length;
            data[5] = (byte)descBytes.Length;
            data[6] = (byte)srcBytes.Length;
            data[7] = 1; // Extension Version

            Array.Copy(idBytes, 0, data, 8, idBytes.Length);
            Array.Copy(descBytes, 0, data, 8 + idBytes.Length, descBytes.Length);
            Array.Copy(srcBytes, 0, data, 8 + idBytes.Length + descBytes.Length, srcBytes.Length);

            return data;
        }

        public static byte[] CreateRR()
        {
            // RR - Rock Ridge
            byte[] data = new byte[5];
            data[0] = (byte)'R';
            data[1] = (byte)'R';
            data[2] = 5;
            data[3] = 1;
            data[4] = 0; // Flags (none for now)
            return data;
        }

        public static byte[] CreatePX(int mode, int links, int uid, int gid)
        {
            // PX - POSIX Attributes
            // Mode (8), Links (8), UID (8), GID (8) = 32 bytes + header 4 = 36 bytes?
            // Actually PX usually has 8 bytes for each field (both endian) -> 32 bytes payload.
            // Header: 4 bytes. Total 36 bytes.
            // But wait, standard PX is:
            // Mode: 8 bytes (both endian)
            // Links: 8 bytes (both endian)
            // User ID: 8 bytes (both endian)
            // Group ID: 8 bytes (both endian)
            // Serial Number: 8 bytes (optional) - we skip

            int len = 4 + 8 + 8 + 8 + 8;
            byte[] data = new byte[len];
            data[0] = (byte)'P';
            data[1] = (byte)'X';
            data[2] = (byte)len;
            data[3] = 1;

            Array.Copy(IsoUtilities.ToBothEndian(mode), 0, data, 4, 8);
            Array.Copy(IsoUtilities.ToBothEndian(links), 0, data, 12, 8);
            Array.Copy(IsoUtilities.ToBothEndian(uid), 0, data, 20, 8);
            Array.Copy(IsoUtilities.ToBothEndian(gid), 0, data, 28, 8);

            return data;
        }

        public static byte[] CreateSL(string targetPath, bool isDirectory)
        {
            // SL - Symbolic Link
            // Flags: 1=Continue, 2=Current, 4=Parent, 8=Root, 16=VolRoot, 32=Host
            // Component Record: Flags (1), Len (1), Component Content (Len)

            // We need to split the path by '/'
            string[] components = targetPath.Split('/');
            List<byte> componentBytes = new List<byte>();

            foreach (var comp in components)
            {
                if (string.IsNullOrEmpty(comp)) continue; // Skip empty (e.g. leading slash handled differently?)

                // If path starts with /, we might need to handle it. 
                // But usually symlinks are relative or absolute.
                // For now, assume relative or absolute path string.

                byte flags = 0;
                if (comp == ".")
                {
                    flags = 2; // Current directory
                    componentBytes.Add(flags);
                    componentBytes.Add(0); // Length 0
                }
                else if (comp == "..")
                {
                    flags = 4; // Parent directory
                    componentBytes.Add(flags);
                    componentBytes.Add(0); // Length 0
                }
                else
                {
                    byte[] content = Encoding.ASCII.GetBytes(comp);
                    componentBytes.Add(0); // Normal component
                    componentBytes.Add((byte)content.Length);
                    componentBytes.AddRange(content);
                }
            }

            int len = 5 + componentBytes.Count;
            byte[] data = new byte[len];
            data[0] = (byte)'S';
            data[1] = (byte)'L';
            data[2] = (byte)len;
            data[3] = 1;
            data[4] = 0; // Flags (0 for now)

            Array.Copy(componentBytes.ToArray(), 0, data, 5, componentBytes.Count);
            return data;
        }

        public static byte[] CreateNM(string name)
        {
            // NM - Alternate Name
            // Flags: 1=Continue, 2=Current, 4=Parent... 
            // Actually NM flags: 1=Continue, 2=Current, 4=Parent... No.
            // NM Flags: 0=Continue, 1=Current, 2=Parent, 4=Host...
            // Wait, NM is for the filename.
            // Flags: 1 = Continue, 2 = Current, 4 = Parent.
            // Usually we just use 0 (Normal name).

            byte[] nameBytes = Encoding.UTF8.GetBytes(name); // UTF8 allowed in modern NM? Or just ASCII? 
                                                             // Rock Ridge uses ISO 9660 charset usually, but NM allows arbitrary.

            int len = 5 + nameBytes.Length;
            byte[] data = new byte[len];
            data[0] = (byte)'N';
            data[1] = (byte)'M';
            data[2] = (byte)len;
            data[3] = 1;
            data[4] = 0; // Flags

            Array.Copy(nameBytes, 0, data, 5, nameBytes.Length);
            return data;
        }
    }
}

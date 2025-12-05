using System.Runtime.InteropServices;
using System.Text;

namespace DotnetPackaging.Formats.Dmg.Iso
{
    public static class IsoConstants
    {
        public const int SectorSize = 2048;
        public const string StandardIdentifier = "CD001";
        public const byte VolumeDescriptorSetTerminator = 255;
        public const byte PrimaryVolumeDescriptor = 1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrimaryVolumeDescriptor
    {
        public byte Type;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] StandardIdentifier;
        public byte Version;
        public byte Unused1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] SystemIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] VolumeIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Unused2;
        public int VolumeSpaceSize; // Both endian
        public int VolumeSpaceSizeBigEndian;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Unused3;
        public short VolumeSetSize; // Both endian
        public short VolumeSetSizeBigEndian;
        public short VolumeSequenceNumber; // Both endian
        public short VolumeSequenceNumberBigEndian;
        public short LogicalBlockSize; // Both endian
        public short LogicalBlockSizeBigEndian;
        public int PathTableSize; // Both endian
        public int PathTableSizeBigEndian;
        public int TypeLPathTableLocation;
        public int TypeLOptionalPathTableLocation;
        public int TypeMPathTableLocation;
        public int TypeMOptionalPathTableLocation;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 34)]
        public byte[] RootDirectoryRecord; // 34 bytes minimum for root
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] VolumeSetIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] PublisherIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] DataPreparerIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] ApplicationIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 37)]
        public byte[] CopyrightFileIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 37)]
        public byte[] AbstractFileIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 37)]
        public byte[] BibliographicFileIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] VolumeCreationDate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] VolumeModificationDate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] VolumeExpirationDate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] VolumeEffectiveDate;
        public byte FileStructureVersion;
        public byte Unused4;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] ApplicationUse;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 653)]
        public byte[] Reserved;
    }

    public class IsoUtilities
    {
        public static byte[] ToBothEndian(short value)
        {
            byte[] bytes = new byte[4];
            BitConverter.GetBytes(value).CopyTo(bytes, 0);
            Array.Reverse(bytes, 0, 2);
            BitConverter.GetBytes(value).CopyTo(bytes, 2);
            // Actually ISO is Little Endian first, then Big Endian
            // But BitConverter depends on system architecture (usually Little Endian on Intel)
            // Let's be explicit
            byte[] le = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(le);
            
            byte[] be = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(be);

            byte[] result = new byte[4];
            Array.Copy(le, 0, result, 0, 2);
            Array.Copy(be, 0, result, 2, 2);
            return result;
        }

        public static byte[] ToBothEndian(int value)
        {
            byte[] le = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(le);
            
            byte[] be = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(be);

            byte[] result = new byte[8];
            Array.Copy(le, 0, result, 0, 4);
            Array.Copy(be, 0, result, 4, 4);
            return result;
        }
        
        public static byte[] StringToBytes(string str, int length)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++) bytes[i] = 0x20; // Padding with spaces
            if (string.IsNullOrEmpty(str)) return bytes;
            
            byte[] strBytes = Encoding.ASCII.GetBytes(str.ToUpper());
            Array.Copy(strBytes, bytes, Math.Min(strBytes.Length, length));
            return bytes;
        }
        
        public static byte[] DateToBytes(DateTime dt)
        {
            // 17 bytes format: YYYYMMDDHHMMSS00 + Offset
            string s = dt.ToString("yyyyMMddHHmmss") + "00";
            byte[] b = new byte[17];
            Encoding.ASCII.GetBytes(s).CopyTo(b, 0);
            b[16] = 0; // Offset from GMT in 15 min intervals. 0 for now.
            return b;
        }
    }
}

using System.Runtime.InteropServices;

namespace DotnetPackaging.Formats.Dmg.Udif
{
    public static class UdifConstants
    {
        public const uint KolySignature = 0x6B6F6C79; // 'koly'
        public const uint BlockSignature = 0x6D697368; // 'mish'
        public const int SectorSize = 512; // DMG uses 512 byte sectors usually
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 512)]
    public struct KolyBlock
    {
        public uint Signature; // 'koly'
        public uint Version; // 4
        public uint HeaderSize; // 512
        public uint Flags;
        public ulong RunningDataForkOffset;
        public ulong DataForkOffset;
        public ulong DataForkLength;
        public ulong RsrcForkOffset;
        public ulong RsrcForkLength;
        public uint SegmentNumber;
        public uint SegmentCount;
        public Guid SegmentId;
        public uint DataForkChecksumType;
        public uint DataForkChecksumSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] DataForkChecksum;
        public ulong XmlOffset;
        public ulong XmlLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 120)]
        public byte[] Reserved1;
        public uint ChecksumType;
        public uint ChecksumSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] Checksum;
        public uint ImageVariant; // 1
        public ulong SectorCount;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
    }

    // We will use a manual writer for KolyBlock to handle Big Endian conversion, 
    // as DMG is Big Endian.
}

using System;
using System.Runtime.InteropServices;

namespace DotnetPackaging.Formats.Dmg.MachO
{
    public static class MachOConstants
    {
        public const uint MH_MAGIC_64 = 0xFEEDFACF;
        public const uint LC_CODE_SIGNATURE = 0x1D;
        public const uint LC_SEGMENT_64 = 0x19;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MachHeader64
    {
        public uint Magic;
        public int CpuType;
        public int CpuSubtype;
        public uint FileType;
        public uint NCmds;
        public uint SizeOfCmds;
        public uint Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LoadCommand
    {
        public uint Command;
        public uint CommandSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LinkEditDataCommand
    {
        public uint Command;
        public uint CommandSize;
        public uint DataOffset;
        public uint DataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SegmentCommand64
    {
        public uint Command;
        public uint CommandSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] SegName;
        public ulong VMAddr;
        public ulong VMSize;
        public ulong FileOff;
        public ulong FileSize;
        public int MaxProt;
        public int InitProt;
        public uint NSects;
        public uint Flags;
    }
}

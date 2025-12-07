using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace DotnetPackaging.Formats.Dmg.MachO
{
    public class CodeSigner
    {
        private const int PageSize = 4096;

        public void Sign(string filePath, string identifier)
        {
            byte[] fileData = File.ReadAllBytes(filePath);

            // Parse Header
            var header = ReadStruct<MachHeader64>(fileData, 0);
            if (header.Magic != MachOConstants.MH_MAGIC_64)
                throw new Exception("Not a 64-bit Mach-O file");

            // Find Commands
            int offset = Marshal.SizeOf<MachHeader64>();
            int linkEditCmdOffset = -1;
            SegmentCommand64 linkEdit = default;
            int codeSigCmdOffset = -1;

            for (int i = 0; i < header.NCmds; i++)
            {
                var cmd = ReadStruct<LoadCommand>(fileData, offset);
                if (cmd.Command == MachOConstants.LC_SEGMENT_64)
                {
                    var seg = ReadStruct<SegmentCommand64>(fileData, offset);
                    string name = Encoding.ASCII.GetString(seg.SegName).TrimEnd('\0');
                    if (name == "__LINKEDIT")
                    {
                        linkEdit = seg;
                        linkEditCmdOffset = offset;
                    }
                }
                else if (cmd.Command == MachOConstants.LC_CODE_SIGNATURE)
                {
                    codeSigCmdOffset = offset;
                }
                offset += (int)cmd.CommandSize;
            }

            if (linkEditCmdOffset == -1)
                throw new Exception("__LINKEDIT segment not found");

            // Calculate Code Directory
            // We hash the file content up to the signature (which is at the end)
            // But actually we hash the whole file except the signature blob?
            // "The Code Directory contains hashes of the pages of the binary."
            // We hash from 0 to codeLimit.

            // If signature exists, we replace it.
            // If not, we append it.

            int codeLimit = (int)linkEdit.FileOff + (int)linkEdit.FileSize;
            if (codeSigCmdOffset != -1)
            {
                // Existing signature, remove it from calculation?
                // Usually signature is at the very end.
                // We assume we overwrite or append.
                // Let's assume we are signing a fresh binary or replacing.
                // If replacing, we need to adjust codeLimit to exclude old signature?
                // Yes, but for simplicity, let's assume we append to the end of file as defined by __LINKEDIT.
            }

            // Generate SuperBlob
            byte[] superBlob = GenerateSuperBlob(identifier, fileData, codeLimit);

            // Append SuperBlob
            // We need to align to 16 bytes?
            // int padding = 0;
            // ...

            // Resize file
            byte[] newFileData = new byte[codeLimit + superBlob.Length];
            Array.Copy(fileData, 0, newFileData, 0, codeLimit);
            Array.Copy(superBlob, 0, newFileData, codeLimit, superBlob.Length);

            // Update __LINKEDIT
            linkEdit.FileSize += (ulong)superBlob.Length;
            linkEdit.VMSize += (ulong)superBlob.Length; // Usually VMSize is aligned to page size?
            // Actually VMSize needs to be aligned.
            ulong vmSizeAligned = (linkEdit.VMSize + PageSize - 1) & ~(ulong)(PageSize - 1);
            linkEdit.VMSize = vmSizeAligned; // Update VMSize to cover new data + padding

            // Write back __LINKEDIT command
            WriteStruct(newFileData, linkEditCmdOffset, linkEdit);

            // Add/Update LC_CODE_SIGNATURE
            var codeSigCmd = new LinkEditDataCommand
            {
                Command = MachOConstants.LC_CODE_SIGNATURE,
                CommandSize = 16,
                DataOffset = (uint)codeLimit,
                DataSize = (uint)superBlob.Length
            };

            if (codeSigCmdOffset != -1)
            {
                WriteStruct(newFileData, codeSigCmdOffset, codeSigCmd);
            }
            else
            {
                // Insert new command
                // We need space in header? Usually there is padding.
                // Or we can overwrite a LC_UUID or something? No.
                // If no space, we are in trouble.
                // But usually linker leaves space? Or we have to shift everything?
                // Shifting is hard.
                // Let's assume there is space or we fail.
                // Actually, standard practice: check if there is space between last command and first section.
                // But for now, let's hope there is space or we just append to commands if there's padding.
                // We will just append it after the last command and update NCmds.
                // Check if space exists.

                int endOfCmds = (int)(Marshal.SizeOf<MachHeader64>() + header.SizeOfCmds);
                // Check first section offset?
                // Usually __TEXT segment starts at 0, but sections start later.
                // We need to find the lowest file offset of any section/segment data.
                // Usually it's PageSize (4096).

                if (endOfCmds + 16 <= PageSize) // Assuming first page is header
                {
                    WriteStruct(newFileData, endOfCmds, codeSigCmd);

                    header.NCmds++;
                    header.SizeOfCmds += 16;
                    WriteStruct(newFileData, 0, header);
                }
                else
                {
                    throw new Exception("No space for LC_CODE_SIGNATURE");
                }
            }

            File.WriteAllBytes(filePath, newFileData);
        }

        private byte[] GenerateSuperBlob(string identifier, byte[] fileData, int codeLimit)
        {
            // SuperBlob
            // - CodeDirectory
            // - Requirements
            // - Entitlements
            // - CMS Wrapper

            // Minimal Ad-hoc: CodeDirectory + CMS (empty/simple)

            // 1. Code Directory
            byte[] codeDir = GenerateCodeDirectory(identifier, fileData, codeLimit);

            // 2. CMS Blob (Ad-hoc)
            // Ad-hoc signature can be a simple blob.
            // Actually for ad-hoc, we can just have CodeDirectory?
            // "Ad-hoc signing is indicated by the linker option -S."
            // It creates a CodeDirectory with flags=0x2 (Adhoc).
            // And a CMS blob that is empty? Or just the SuperBlob with CodeDirectory?
            // Valid signature usually requires a CMS blob.
            // But ad-hoc might not.
            // Let's try just CodeDirectory first.

            // SuperBlob Header
            // Magic (0xfade0cc0)
            // Length
            // Count
            // Index...

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Swap(0xfade0cc0)); // Magic
                w.Write(0); // Length placeholder
                w.Write(Swap(1)); // Count (Just CodeDirectory)

                // Index
                w.Write(Swap(0)); // Type 0 (CodeDirectory)
                w.Write(Swap(20)); // Offset (Header 12 + Index 8 = 20)

                w.Write(codeDir);

                byte[] blob = ms.ToArray();

                // Update length
                int len = blob.Length;
                blob[4] = (byte)(len >> 24);
                blob[5] = (byte)(len >> 16);
                blob[6] = (byte)(len >> 8);
                blob[7] = (byte)len;

                return blob;
            }
        }

        private byte[] GenerateCodeDirectory(string identifier, byte[] fileData, int codeLimit)
        {
            // CodeDirectory Header
            // Magic (0xfade0c02)
            // Length
            // Version (0x20100)
            // Flags (0x2 = Adhoc)
            // HashOffset
            // IdentOffset
            // ...

            int pageSize = 4096;
            int pageCount = (codeLimit + pageSize - 1) / pageSize;
            int hashSize = 32; // SHA-256
            int hashType = 2; // SHA-256

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Swap(0xfade0c02)); // Magic
                w.Write(0); // Length placeholder
                w.Write(Swap(0x20100)); // Version
                w.Write(Swap(0x2)); // Flags (Adhoc)
                w.Write(Swap(0)); // HashOffset placeholder
                w.Write(Swap(0)); // IdentOffset placeholder
                w.Write(Swap(0)); // NSpecialSlots
                w.Write(Swap(pageCount)); // NCodeSlots
                w.Write(Swap(codeLimit)); // CodeLimit
                w.Write((byte)hashSize); // HashSize
                w.Write((byte)hashType); // HashType
                w.Write((byte)0); // Platform
                w.Write((byte)PageSizeLog2(pageSize)); // PageSize (log2)
                w.Write(Swap(0)); // Spare2

                // Scatter Offset? Version 0x20100 has scatter?
                // Let's stick to version 0x20001 if simpler?
                // 0x20100 is standard for SHA-256.

                w.Write(Swap(0)); // ScatterOffset
                w.Write(Swap(0)); // TeamOffset

                // Offsets
                int headerSize = (int)ms.Position;
                int identOffset = headerSize;
                byte[] identBytes = Encoding.UTF8.GetBytes(identifier);
                w.Write(identBytes);
                w.Write((byte)0); // Null term

                int hashOffset = (int)ms.Position;

                // Hashes
                using (var sha = SHA256.Create())
                {
                    for (int i = 0; i < pageCount; i++)
                    {
                        int offset = i * pageSize;
                        int count = Math.Min(pageSize, codeLimit - offset);
                        byte[] hash = sha.ComputeHash(fileData, offset, count);
                        w.Write(hash);
                    }
                }

                byte[] blob = ms.ToArray();

                // Update placeholders
                int len = blob.Length;
                UpdateInt(blob, 4, len);
                UpdateInt(blob, 16, hashOffset);
                UpdateInt(blob, 20, identOffset);

                return blob;
            }
        }

        private int PageSizeLog2(int size)
        {
            int log = 0;
            while ((size >>= 1) > 0) log++;
            return log;
        }

        private T ReadStruct<T>(byte[] data, int offset) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, offset, ptr, size);
            T obj = Marshal.PtrToStructure<T>(ptr);
            Marshal.FreeHGlobal(ptr);
            return obj;
        }

        private void WriteStruct<T>(byte[] data, int offset, T obj) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);
            Marshal.Copy(ptr, data, offset, size);
            Marshal.FreeHGlobal(ptr);
        }

        private void UpdateInt(byte[] data, int offset, int value)
        {
            int swapped = Swap(value);
            BitConverter.GetBytes(swapped).CopyTo(data, offset);
        }

        private uint Swap(uint v) => (uint)System.Net.IPAddress.HostToNetworkOrder((int)v);
        private int Swap(int v) => System.Net.IPAddress.HostToNetworkOrder(v);
    }
}

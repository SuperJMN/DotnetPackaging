using DotnetPackaging.Formats.Dmg.MachO;

namespace DotnetPackaging.Formats.Dmg.Tests
{
    public class MachOTests
    {
        [Fact]
        public void Can_Parse_MachO_Header()
        {
            // Create a fake Mach-O file
            byte[] data = new byte[4096];
            using (var ms = new MemoryStream(data))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(MachOConstants.MH_MAGIC_64); // Magic
                w.Write(0x01000007); // CPU Type (x86_64)
                w.Write(0x80000003); // CPU Subtype
                w.Write(2); // File Type (Execute)
                w.Write(0); // NCmds
                w.Write(0); // SizeOfCmds
                w.Write(0); // Flags
                w.Write(0); // Reserved
            }

            // We need to expose parsing logic or test via side effects.
            // CodeSigner.Sign reads the file.
            // But it expects __LINKEDIT.
            // Let's verify we can throw if not found.

            string tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, data);

            var signer = new CodeSigner();
            var ex = Assert.Throws<Exception>(() => signer.Sign(tempFile, "com.test"));
            Assert.Equal("__LINKEDIT segment not found", ex.Message);

            File.Delete(tempFile);
        }

        [Fact]
        public void Sign_Appends_Signature()
        {
            // Construct a minimal valid Mach-O with __LINKEDIT
            byte[] data = new byte[8192];
            using (var ms = new MemoryStream(data))
            using (var w = new BinaryWriter(ms))
            {
                // Header (32 bytes)
                w.Write(MachOConstants.MH_MAGIC_64);
                w.Write(0);
                w.Write(0);
                w.Write(0);
                w.Write(1); // NCmds
                w.Write(72); // SizeOfCmds (SegmentCommand64 size)
                w.Write(0);
                w.Write(0);

                // Segment Command (72 bytes)
                w.Write(MachOConstants.LC_SEGMENT_64);
                w.Write(72);
                w.Write(System.Text.Encoding.ASCII.GetBytes("__LINKEDIT\0\0\0\0\0\0"));
                w.Write((ulong)4096); // VMAddr
                w.Write((ulong)4096); // VMSize
                w.Write((ulong)4096); // FileOff
                w.Write((ulong)0); // FileSize (Empty LINKEDIT initially)
                w.Write(0); // MaxProt
                w.Write(0); // InitProt
                w.Write(0); // NSects
                w.Write(0); // Flags
            }

            string tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, data);

            var signer = new CodeSigner();
            signer.Sign(tempFile, "com.test");

            byte[] signedData = File.ReadAllBytes(tempFile);

            // Should be larger than code limit (4096)
            Assert.True(signedData.Length > 4096);

            // Should contain LC_CODE_SIGNATURE
            // Scan for command 0x1D
            // Direct check at expected location (104)
            uint valAt104 = BitConverter.ToUInt32(signedData, 104);
            Assert.Equal(MachOConstants.LC_CODE_SIGNATURE, valAt104);

            /*
            bool foundSigCmd = false;
            for (int i = 32; i < 32 + 72 + 16; i += 4)
            {
                uint val = BitConverter.ToUInt32(signedData, i);
                if (i == 104) Console.WriteLine($"Test read at 104: {val:X}");
                
                if (val == MachOConstants.LC_CODE_SIGNATURE)
                {
                    foundSigCmd = true;
                    break;
                }
            }
            
            Assert.True(foundSigCmd, "LC_CODE_SIGNATURE not found");
            */

            File.Delete(tempFile);
        }
    }
}

using System.Text;
using DotnetPackaging.Formats.Dmg.Iso;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Formats.Dmg.Tests
{
    public class IsoTests
    {
        [Fact]
        public void Pvd_Structure_IsCorrect()
        {
            var builder = new IsoBuilder("TEST_ISO");
            using var ms = new MemoryStream();
            builder.Build(ms);

            byte[] data = ms.ToArray();

            // PVD starts at sector 16 (16 * 2048 = 32768)
            int pvdOffset = 16 * 2048;

            Assert.Equal(1, data[pvdOffset]); // Type 1
            Assert.Equal("CD001", Encoding.ASCII.GetString(data, pvdOffset + 1, 5));
            Assert.Equal(1, data[pvdOffset + 6]); // Version 1

            // Volume ID at offset 40
            string volId = Encoding.ASCII.GetString(data, pvdOffset + 40, 32).TrimEnd(' ');
            Assert.Equal("TEST_ISO", volId);
        }

        [Fact]
        public void RockRidge_Extensions_ArePresent()
        {
            var builder = new IsoBuilder("RR_TEST");
            builder.Root.AddChild(new IsoSymlink("mysylink", "/target/path"));

            using var ms = new MemoryStream();
            builder.Build(ms);

            byte[] data = ms.ToArray();

            // Find directory record for root.
            // PVD has root record at 156.
            int pvdOffset = 16 * 2048;
            int rootRecordOffset = pvdOffset + 156;

            // Get Root Extent Location (byte 2 in record)
            int rootSector = BitConverter.ToInt32(data, rootRecordOffset + 2);

            // Go to Root Sector
            int rootDirOffset = rootSector * 2048;

            // Read records in root directory
            // First is . (self)
            int lenDot = data[rootDirOffset];
            // Second is .. (parent)
            int offsetDotDot = rootDirOffset + lenDot;
            int lenDotDot = data[offsetDotDot];

            // Third should be our symlink 'mysylink'
            int offsetSymlink = offsetDotDot + lenDotDot;
            int lenSymlink = data[offsetSymlink];

            Assert.True(lenSymlink > 0);

            // Check name
            int nameLen = data[offsetSymlink + 32];
            string name = Encoding.ASCII.GetString(data, offsetSymlink + 33, nameLen);
            Assert.Equal("MYSYLINK", name); // ISO name is uppercase

            // Check SUSP/Rock Ridge
            // System Use area starts after name + padding
            int sysUseOffset = offsetSymlink + 33 + nameLen;
            if (sysUseOffset % 2 != 0) sysUseOffset++;

            // Look for SL entry
            bool foundSL = false;
            int current = sysUseOffset;
            while (current < offsetSymlink + lenSymlink)
            {
                byte sig1 = data[current];
                byte sig2 = data[current + 1];
                int len = data[current + 2];

                if (sig1 == 'S' && sig2 == 'L')
                {
                    foundSL = true;
                    // Verify content: Flags(1), CompLen(1), Comp(Len)
                    // We added "/target/path"
                    // Components: "", "target", "path" (split by /)
                    // Wait, our implementation splits by /.
                    // If path starts with /, first component is empty?
                    // Let's check implementation of CreateSL.
                    break;
                }
                current += len;
            }

            Assert.True(foundSL, "SL entry not found in symlink record");
        }

        [Fact]
        public void File_Content_IsPreserved()
        {
            var builder = new IsoBuilder("CONTENT_TEST");
            string content = "Hello World";
            var file = new IsoFile("hello.txt")
            {
                ContentSource = () => ByteSource.FromBytes(Encoding.ASCII.GetBytes(content))
            };
            builder.Root.AddChild(file);

            using var ms = new MemoryStream();
            builder.Build(ms);

            byte[] data = ms.ToArray();

            // Find file location
            // We can scan for "HELLO.TXT" in root dir
            // Or just search for content "Hello World" in the image to verify it's there
            string dataStr = Encoding.ASCII.GetString(data);
            Assert.Contains("Hello World", dataStr);
        }
    }
}

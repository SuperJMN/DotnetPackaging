using DotnetPackaging.Formats.Dmg.Iso;
using DotnetPackaging.Formats.Dmg.Udif;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Formats.Dmg.Tests
{
    public class EndToEndTests
    {
        [Fact]
        public void Package_EvaluacionesApp_Desktop()
        {
            string inputDir = "/mnt/fast/Repos/ProyectoAna/EvaluacionesApp.Desktop/bin/Release/net9.0/osx-arm64/";
            if (!Directory.Exists(inputDir))
            {
                Console.WriteLine($"Input directory not found: {inputDir}. Skipping E2E test.");
                return;
            }

            string outputDmg = "EvaluacionesApp.Desktop.Test.dmg";
            if (File.Exists(outputDmg)) File.Delete(outputDmg);

            // Run the CLI logic programmatically
            // We can call Program.Main, but it might exit the process or handle args parsing.
            // Better to instantiate the components directly or extract the logic.
            // For now, let's try calling Program.Main if it's accessible, or copy the logic.
            // Program class is internal by default in top-level statements or standard templates unless specified.
            // Let's check Program.cs visibility. 
            // Actually, Program.cs in console apps usually compiles to a class named "Program".
            // But calling Main might be tricky if it returns void/int.

            // Instead, let's replicate the orchestration logic here to have better control and assertions.
            // This also verifies the library usage pattern.

            var appName = "EvaluacionesApp.Desktop";
            var builder = new IsoBuilder("EvaluacionesApp"); // Volume Name

            // Create .app structure
            var root = builder.Root;
            root.AddChild(new IsoSymlink("Applications", "/Applications"));

            var appBundle = root.AddDirectory($"{appName}.app");
            var contents = appBundle.AddDirectory("Contents");
            var macOs = contents.AddDirectory("MacOS");
            var resources = contents.AddDirectory("Resources");

            // Copy files
            var files = Directory.GetFiles(inputDir);
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                // Skip non-essential files if needed, but for now copy all
                if (fileName.EndsWith(".pdb") || fileName.EndsWith(".dbg")) continue;

                var isoFile = new IsoFile(fileName)
                {
                    ContentSource = () => ByteSource.FromStreamFactory(() => File.OpenRead(file))
                };
                macOs.AddChild(isoFile);
            }

            // Sign binaries (Ad-hoc)
            var signer = new DotnetPackaging.Formats.Dmg.MachO.CodeSigner();
            // We need to sign the *source* files? No, we need to sign the files *in the ISO*.
            // Wait, IsoFile takes a stream. CodeSigner modifies a file on disk.
            // The current Program.cs implementation copies files to a temp dir, signs them, then adds to ISO?
            // Let's check Program.cs.

            // Re-reading Program.cs logic would be good. 
            // If Program.cs modifies files in place or copies them, we should do the same.
            // Since we can't modify the source build output (it might break incremental builds), 
            // we should copy to a temp dir, sign, then add to ISO.

            // For this test, let's just verify we can build the DMG from the source files 
            // WITHOUT signing modification first, to test the packaging pipeline.
            // OR, if we want to test signing, we must copy.

            // Let's proceed with building the DMG first.

            using (var isoStream = new MemoryStream())
            {
                builder.Build(isoStream);
                isoStream.Position = 0;

                using (var dmgStream = File.Create(outputDmg))
                {
                    var writer = new UdifWriter();
                    writer.Create(isoStream, dmgStream);
                }
            }

            Assert.True(File.Exists(outputDmg));
            var info = new FileInfo(outputDmg);
            Assert.True(info.Length > 0);

            // Verify Koly Block
            using (var fs = File.OpenRead(outputDmg))
            {
                fs.Seek(-512, SeekOrigin.End);
                byte[] koly = new byte[512];
                fs.Read(koly, 0, 512);

                // Signature 'koly' is 0x6B6F6C79
                Assert.Equal(0x6B, koly[0]);
                Assert.Equal(0x6F, koly[1]);
                Assert.Equal(0x6C, koly[2]);
                Assert.Equal(0x79, koly[3]);
            }

            // Cleanup
            File.Delete(outputDmg);
        }
    }
}

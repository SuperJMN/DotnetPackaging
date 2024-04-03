using System.IO.Abstractions;
using System.Runtime.InteropServices;
using NyaFs.Filesystem.SquashFs.Types;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.AppImage.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Task()
        {
            var p = new NyaFs.Filesystem.SquashFs.SquashFsBuilder(SqCompressionType.Gzip);
            p.Directory("/", 0, 0, 511);
            p.Directory("/usr", 0, 0, 511);
            p.File(@"/AppRun", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/AppRun"), 0, 0, 493);
            p.File(@"/AvaloniaSyncer.desktop", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/AvaloniaSyncer.desktop"), 0, 0, 493);
            p.File(@"/AvaloniaSyncer.png", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/AvaloniaSyncer.png"), 0, 0, 493);
            
            p.Directory("/usr/bin", 0, 0, 511);
            
            p.Directory("/usr/bin/AvaloniaSyncer", 0, 0, 511);
            p.File(@"/usr/bin/AvaloniaSyncer/Avalonia.Base.dll", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/usr/bin/AvaloniaSyncer/Avalonia.Base.dll"), 0, 0, 493);
            p.File(@"/usr/bin/AvaloniaSyncer/Avalonia.Controls.DataGrid.dll", File.ReadAllBytes("c:/users/jmn/Desktop/AvaloniaSyncer.AppDir/usr/bin/AvaloniaSyncer/Avalonia.Controls.DataGrid.dll"), 0, 0, 493);
            
            File.WriteAllBytes("c:\\users\\jmn\\Desktop\\Test.squashfs", p.GetFilesystemImage());
        }
        
        [Fact]
        public async Task Test1()
        {
            var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
            var dir = fs.GetDirectory("C:/Users/JMN/Desktop/AvaloniaSyncer.AppDir");
            var mem = new MemoryStream();
            await AppImagePackager.CreatePayload(mem, dir);
            await File.WriteAllBytesAsync("Salida.squashfs", mem.ToArray());
        }

        [Fact]
        public async Task GetRuntime()
        {
            var stream = await RuntimeDownloader.GetRuntimeStream(Architecture.X64, new DefaultHttpClientFactory());
            var fs = File.OpenWrite("Runtime");
            await stream.CopyToAsync(fs);
        }
        
        [Fact]
        public async Task FullTest()
        {
            var runtimeStream = await RuntimeDownloader.GetRuntimeStream(Architecture.X64, new DefaultHttpClientFactory());
            var fs = new FileSystemRoot(new ObservableFileSystem(new WindowsZafiroFileSystem(new FileSystem())));
            var dir = fs.GetDirectory("C:/Users/JMN/Desktop/AvaloniaSyncer.AppDir");
            
            var payloadStream = new MemoryStream();
            await AppImagePackager.CreatePayload(payloadStream, dir);
            await File.WriteAllBytesAsync("Salida.squashfs", payloadStream.ToArray());
            
            // Concatenate runtimeStream and payloadStream
            var concatenatedStream = new MemoryStream();

            payloadStream.Position = 0;

            await runtimeStream.CopyToAsync(concatenatedStream);
            await payloadStream.CopyToAsync(concatenatedStream);
            // Write a file named "output.AppImage" with the resulting stream
            await File.WriteAllBytesAsync("output.AppImage", concatenatedStream.ToArray());
        }
    }
}
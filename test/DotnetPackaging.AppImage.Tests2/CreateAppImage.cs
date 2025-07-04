using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.WIP;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;
using Directory = Zafiro.DivineBytes.Directory;
using File = Zafiro.DivineBytes.File;

namespace DotnetPackaging.AppImage.Tests2;

public class CreateAppImage
{
    [Fact]
    public async Task Create()
    {
        
        var file = new File("Hola", ByteSource.FromString("Hola Mundo", Encoding.UTF8));
        
        IEnumerable<IDirectory> directories = new List<IDirectory>();
        IEnumerable<File> files = [file];
        var dir = new Directory("", files, directories);
        var unixDir = dir.ToUnixDirectory();
        
        var save = await RuntimeFactory.Create(Architecture.X64)
            .Map(rt => new WIP.AppImage(rt, unixDir))
            .Bind(image => image.ToByteSource()
                .Bind(source => source.WriteTo("/home/jmn/Escritorio/AppImage.appimage")));

        save.Should().Succeed();
    }
}
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.Core;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Tests;

public class SquashFSTests
{
    [Fact]
    public async Task Create_SquashFS()
    {
        var root = new UnixDirectory("", 0, new UnixPermissions(), [], [UnixFile()]);

        var result = await SquashFS.Create(root)
            .Bind(data => data.WriteTo("/home/jmn/Escritorio/File.squashfs"));
        result.Should().Succeed();
    }

    private static UnixFile UnixFile()
    {
        var contents = ByteSource.FromString("Hola", Encoding.UTF8);
        var unixPermissions = new UnixPermissions(Permission.All);
        return new UnixFile(new Resource("File", contents), unixPermissions, 0);
    }
}
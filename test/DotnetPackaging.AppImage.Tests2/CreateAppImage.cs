using System.Text.Json;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.WIP;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Tests2;

public class CreateAppImage
{
    [Fact]
    public async Task Create()
    {
        var containerResult = new Dictionary<string, IByteSource>()
        {
            ["home/jmn/Desktop/Hola.txt"] = ByteSource.FromString("Contents"),
            ["home/jmn/config.json"] = ByteSource.FromString(JsonSerializer.Serialize(new MyClass()
            {
                Name = "JMN",
                Value = 42,
            })),
            ["Sample.md"] = ByteSource.FromString("**This is a sample file**"),
        }.ToDirectoryTree();

        var appImage = 
            await from rt in RuntimeFactory.Create(Architecture.X64)
            from container in containerResult.Map(container => container.ToUnixDirectory())
            from unixDir in Result.Try(() => container.ToUnixDirectory())
            select new WIP.AppImage(rt, unixDir);

        Result save = await appImage
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo("/home/jmn/Escritorio/AppImage.appimage"));

        save.Should().Succeed();
    }
}

public class MyClass
{
    public string Name { get; set; }
    public int Value { get; set; }
}
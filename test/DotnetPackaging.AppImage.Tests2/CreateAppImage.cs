using System.IO.Abstractions;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DotnetPackaging.AppImage.WIP;
using FluentAssertions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using Zafiro.DivineBytes.Unix;

namespace DotnetPackaging.AppImage.Tests2;

public class CreateAppImage
{
    [Fact]
    public async Task Create_testing_appimage()
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
        }.ToRootContainer(); // Use root container without artificial name

        var appImage = 
            from rt in Result.Success(new Runtime(ByteSource.FromString("THIS IS A RUNTIME")))
            from rootContainer in containerResult
            from unixDir in Result.Try(() => rootContainer.AsContainer().ToUnixDirectory())
            select new WIP.AppImage(rt, unixDir);

        Result save = await appImage
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo("/home/jmn/Escritorio/AppImage.appimage"));

        save.Should().Succeed();
    }

    [Fact]
    public async Task Create_compliant_appimage()
    {
        var fileSystem = new FileSystem();
        var files = new DirContainer(fileSystem.DirectoryInfo.New("TestFiles/Minimal"));
        var root = files.AsRoot();
        
        var builder = new AppImageBuilder();
        var appImage = builder.Create(root, "SampleApp");

        var save = await appImage
            .Bind(x => x.ToByteSource())
            .Bind(source => source.WriteTo("/home/jmn/Escritorio/AppImageCompliant.appimage"));

        save.Should().Succeed();
    }
}

public class MyClass
{
    public string Name { get; set; }
    public int Value { get; set; }
}
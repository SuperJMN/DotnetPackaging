using DotnetPackaging.Deb.Archives.Deb;
using DotnetPackaging;
using Zafiro.DataModel;
using Zafiro.DivineBytes;

namespace DotnetPackaging.Deb.Tests.Integration;

public class DebTests
{
    [Fact]
    public async Task Integration()
    {
        var files = new Dictionary<string, IByteSource>
        {
            ["App"] = ByteSource.FromString("#!/bin/bash\necho 'hello'"),
            ["readme.txt"] = ByteSource.FromString("Sample app payload"),
        };

        var containerResult = files.ToRootContainer();
        containerResult.Should().Succeed();

        var icon = new DummyIcon();

        var result = await DebFile.From()
            .Container(containerResult.Value, "SampleApp")
            .Configure(setup =>
            {
                setup.WithName("Sample App")
                    .WithPackage("sample-app")
                    .WithVersion("1.0.0")
                    .WithExecutableName("App")
                    .WithArchitecture(Architecture.X64)
                    .WithIcon(icon)
                    .WithComment("Hi");
            })
            .Build();

        result.Should().Succeed();
    }

    private sealed class DummyIcon : IIcon
    {
        private readonly IData data = Data.FromByteArray([0x89, 0x50, 0x4E, 0x47]);

        public IObservable<byte[]> Bytes => data.Bytes;

        public long Length => data.Length;

        public int Size { get; } = 64;
    }
}

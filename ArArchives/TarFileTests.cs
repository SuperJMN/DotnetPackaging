using FluentAssertions;
using Serilog;
using Xunit.Abstractions;
using Logger = Serilog.Core.Logger;

namespace Archive.Tests;

public class TarFileTests
{
    private readonly Logger logger;

    public TarFileTests(ITestOutputHelper output)
    {
        logger = new LoggerConfiguration()
            .WriteTo.TestOutput(output)
            .CreateLogger();
    }

    [Fact]
    public void Create()
    {
        var rawStream = new MemoryStream();
        new TarData(new LoggingByteWriter(new ByteWriter(rawStream), logger)).Build();

        var copy = new byte[2048];
        rawStream.ToArray().CopyTo(copy, 0);
        copy.Should().BeEquivalentTo(File.ReadAllBytes("control.tar"));
    }
}
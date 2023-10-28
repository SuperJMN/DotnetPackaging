using Archiver;
using Archiver.Tar;
using FluentAssertions;
using MoreLinq;
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
    public void Create1()
    {
        var rawStream = new MemoryStream();
        new Tar(rawStream).Build(new Entry("control", new MemoryStream("""
                                                                       Package: avaloniasyncer
                                                                       Priority: optional
                                                                       Section: utils
                                                                       Maintainer: SuperJMN
                                                                       Version: 2.0.4
                                                                       Homepage: http://www.superjmn.com
                                                                       Vcs-Git: git://github.com/zkSNACKs/WalletWasabi.git
                                                                       Vcs-Browser: https://github.com/zkSNACKs/WalletWasabi
                                                                       Architecture: amd64
                                                                       License: MIT
                                                                       Installed-Size: 207238
                                                                       Recommends: policykit-1
                                                                       Description: open-source, non-custodial, privacy focused Bitcoin wallet
                                                                         Built-in Tor, coinjoin, payjoin and coin control features.

                                                                       """.FromCrLfToLf().ToAscii())));

        var result = rawStream.ToArray();
        result.Should().BeEquivalentTo(File.ReadAllBytes("TestFiles\\control.tar"));
    }


    [Fact]
    public void Simple()
    {
        var rawStream = new MemoryStream();
        new Tar(rawStream).Build(
            new Entry("File1.txt", new MemoryStream("Hola".ToAscii())));

        var result = rawStream.ToArray();
        var expectedBytes = File.ReadAllBytes("TestFiles\\Sample.tar");

        LogComparison(result, expectedBytes);

        result.Should().BeEquivalentTo(expectedBytes);
    }

    [Fact]
    public void Complex()
    {
        var rawStream = new MemoryStream();
        new Tar(rawStream).Build(
            new Entry("File1.txt", new MemoryStream("Hola".ToAscii())),
            new Entry("File2.txt", new MemoryStream("Adiós".ToAscii()))
        );

        var result = rawStream.ToArray();

        var expectedBytes = File.ReadAllBytes("TestFiles\\Sample.tar");

        LogComparison(result, expectedBytes);

        result.Should().BeEquivalentTo(expectedBytes);
    }

    private void LogComparison(byte[] result, byte[] expectedBytes)
    {
        result.AsEnumerable()
            .Zip(expectedBytes)
            .Select((tuple, i) => new { tuple, i })
            .ForEach(tuple =>
            {
                var isMatch = tuple.tuple.First == tuple.tuple.Second ? "X" : " ";
                logger.Information("[{Position}] [{Result}] Expecting: {Expected} - Got: {Actual}", tuple.i, isMatch, tuple.tuple.First, tuple.tuple.Second);
            });
    }
}
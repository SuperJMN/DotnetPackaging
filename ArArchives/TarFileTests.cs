using Archiver;
using Archiver.Tar;
using FluentAssertions;
using MoreLinq;
using Serilog;
using System;
using Serilog.Events;
using Xunit.Abstractions;
using Logger = Serilog.Core.Logger;

namespace Archive.Tests;

public class TarFileTests
{
    private readonly Logger logger;

    public TarFileTests(ITestOutputHelper output)
    {
        logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output, LogEventLevel.Debug)
            .CreateLogger();
    }

    [Fact]
    public void Create1()
    {
        var rawStream = new MemoryStream();
        new Tar(rawStream, logger).Build(new Entry("control", new MemoryStream("""
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
        new Tar(rawStream, logger).Build(
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
        new Tar(rawStream, logger).Build(
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
        logger.Debug("Result: {Length} items. Expected: {Length} bytes", result.Length, expectedBytes.Length);

        var comparison = result.Cast<byte?>()
            .ZipLongest(expectedBytes.Cast<byte?>(), (one, another) => new { one, another })
            .Select((x, i) => new { x.one, x.another, i, isMatch = Equals(x.one, x.another) })
            .ToList();

        var matches = comparison.Count(x => x.isMatch);
        var total = comparison.Count;
        logger.Information("Matched {Matches} out of {Total} ({Percent:P})", matches, total, (double)matches/total);

        comparison
            .ForEach(x =>
            {
                logger.Information("[{Position}] [{Result}] Expecting: {Expected} - Got: {Actual}", x.i, x.isMatch ? "X" : " ", x.one, x.another);
            });
    }
}
using Archiver;
using Archiver.Tar;
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
        new Tar(rawStream).Build("control", new MemoryStream(Content().ToAscii()));

        var copy = new byte[2048];
        rawStream.ToArray().CopyTo(copy, 0);
        copy.Should().BeEquivalentTo(File.ReadAllBytes("control.tar"));
    }

    public string Content()
    {
        return """
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

               """.FromCrLfToLf();
    }
}
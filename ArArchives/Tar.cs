using System.Reactive.Linq;
using System.Text;
using Archiver;
using CSharpFunctionalExtensions;
using Zafiro.IO;

namespace Archive.Tests;

public static class Mixin
{
    public static IObservable<T> Pack<T>(this IObservable<T> sequence, int blockSize, T paddingItem)
    {
        return sequence
            .Buffer(blockSize)
            .SelectMany(block =>
            {
                int paddingCount = blockSize - block.Count;
                if (paddingCount > 0)
                {
                    var paddingBlock = Enumerable.Range(1, paddingCount).Select(_ => paddingItem);
                    return block.Concat(paddingBlock).ToObservable();
                }
                return block.ToObservable();
            });
    }
}

public class Tar
{
    private readonly Stream output;

    public Tar(Stream output)
    {
        this.output = output;
    }

    public Result Build()
    {
        var header = WriteHeader()
            .Pack<byte>(512, 0);

        header.Sum(b => b).Subscribe(i => {  });

        var content = Content().Pack<byte>(512, 0);

        header.Concat(content)
            .DumpTo(output)
            .Subscribe();

        return Result.Success();
    }

    private IObservable<byte> WriteHeader()
    {
        return Observable.Concat
        (
            Filename("control"),
            FileMode(),
            Owner(),
            Group(),
            FileSize(),
            LastModification(),
            ChecksumPlaceholder(),
            LinkIndicator(),
            NameOfLinkedFile()
        );
    }

    /// <summary>
    /// From 512 Content 
    /// </summary>

    private IObservable<byte> Content()
    {
        var content = """
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

        return ToAscii(content);
    }

    private static IObservable<byte> ToAscii(string content) => Encoding.ASCII.GetBytes(content).ToObservable();

    /// <summary>
    /// From 156 to 157 Link indicator (file type)
    /// </summary>
    private IObservable<byte> LinkIndicator()
    {
        return new byte[] { 0x00, }.ToObservable();
    }

    /// <summary>
    /// From 157 to 257 Link indicator (file type)
    /// </summary>
    private IObservable<byte> NameOfLinkedFile()
    {
        return ToAscii("".ToFixed(100));
    }


    private IObservable<byte> ChecksumPlaceholder()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, }.ToObservable();
    }

    //private IObservable<byte> ChecksumPlaceholder()
    //{
    //    return new byte[] { 0x20, 0x20, 0x20, 0x35, 0x30, 0x33, 0x32, 0x20, }.ToObservable();
    //}

    /// <summary>
    /// From 124 to 136 (in octal)
    /// </summary>
    private IObservable<byte> FileSize()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x36, 0x37, 0x37, 0x00, }.ToObservable();
    }

    /// <summary>
    /// From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private IObservable<byte> LastModification()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00, }.ToObservable();
    }

    /// <summary>
    /// From 116 to 124
    /// </summary>
    private IObservable<byte> Group()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00 }.ToObservable();
    }

    private IObservable<byte> FileMode()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x37, 0x37, 0x37, 0x0 }.ToObservable();
    }

    private IObservable<byte> Owner()
    {
        return new byte[] { 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x30, 0x00, }.ToObservable();
    }

    private IObservable<byte> Filename(string filename)
    {
        return ToAscii(filename.ToFixed(100));
    }
}
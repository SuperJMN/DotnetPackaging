using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Lightweight;
using Zafiro.Reactive;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarEntryMixin
{
    public static IObservableDataStream ToByteProvider(this FileTarEntry entry)
    {
        return new CompositeObservableDataStream(entry.Header(entry.Content.Length, 0).PadToNearestMultiple(512), entry.Content.PadToNearestMultiple(512));
    }

    public static IObservableDataStream ToByteProvider(this DirectoryTarEntry entry)
    {
        return new CompositeObservableDataStream(entry.Header(0, 5).PadToNearestMultiple(512));
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <returns></returns>
    public static IObservableDataStream Filename(this TarEntry entry) => new StringObservableDataStream(entry.Path.ToString().Truncate(100).PadRight(100, '\0'), Encoding.ASCII);

    /// <summary>
    ///     From 100 to 108
    /// </summary>
    /// <returns></returns>
    public static IObservableDataStream FileMode(this TarEntry entry) => new StringObservableDataStream(entry.Properties.FileMode.ToFileModeString().NullTerminatedPaddedField(8), Encoding.ASCII);

    /// <summary>
    ///     From 108 to 116
    /// </summary>
    public static IObservableDataStream Owner(this TarEntry entry) => new StringObservableDataStream(entry.Properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8), Encoding.ASCII);


    /// <summary>
    ///     From 116 to 124
    /// </summary>
    public static IObservableDataStream Group(this TarEntry entry) => new StringObservableDataStream(entry.Properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8), Encoding.ASCII);

    public static IObservableDataStream Header(this TarEntry entry, long fileLength, int linkIndicator)
    {
        var header = entry.HeaderCore(Maybe<long>.None, fileLength, linkIndicator);
        var checkSum = header.Bytes.Flatten().ToEnumerable().Sum(b => (long)b);
        return entry.HeaderCore(checkSum, fileLength, linkIndicator);
    }

    private static IObservableDataStream HeaderCore(this TarEntry entry, Maybe<long> checkSum, long fileLength, int linkIndicator) => new CompositeObservableDataStream
    (
        entry.Filename(),
        entry.FileMode(),
        entry.Owner(),
        entry.Group(),
        FileSize(fileLength),
        entry.LastModification(),
        Checksum(checkSum),
        LinkIndicator(linkIndicator),
        entry.NameOfLinkedFile(),
        Ustar(),
        UstarVersion(),
        entry.OwnerUsername(),
        entry.GroupUsername()
    );

    /// <summary>
    ///     From 124 to 136 (in octal)
    /// </summary>
    private static IObservableDataStream FileSize(long fileLength) => new StringObservableDataStream(fileLength.ToOctalField(), Encoding.ASCII);

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private static IObservableDataStream LastModification(this TarEntry entry) => new StringObservableDataStream(entry.Properties.LastModification.ToUnixTimeSeconds().ToOctalField(), Encoding.ASCII);

    private static IObservableDataStream Checksum(Maybe<long> checksum)
    {
        var content = checksum.Match(l => l.ToOctal().PadLeft(6, '0').NullTerminated() + " ", () => new string(' ', 8));
        return new StringObservableDataStream(content, Encoding.ASCII);
    }

    /// <summary>
    ///     From 157 to 257 Link indicator (file type)
    /// </summary>
    private static IObservableDataStream NameOfLinkedFile(this TarEntry entry) => new StringObservableDataStream(new string('\0', 100), Encoding.ASCII);

    /// <summary>
    ///     From 156 to 157 Link indicator (file type)
    /// </summary>
    private static IObservableDataStream LinkIndicator(int linkIndicator) => new StringObservableDataStream(linkIndicator.ToString(), Encoding.ASCII);

    private static IObservableDataStream Ustar() => new StringObservableDataStream("ustar".PadRight(6, ' '), Encoding.ASCII);

    private static IObservableDataStream UstarVersion() => new ByteArrayObservableDataStream([0x20, 0x0]);

    private static IObservableDataStream OwnerUsername(this TarEntry entry)
    {
        return new StringObservableDataStream(entry.Properties.OwnerUsername.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32)), Encoding.ASCII);
    }

    private static IObservableDataStream GroupUsername(this TarEntry entry)
    {
        return new StringObservableDataStream(entry.Properties.GroupName.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32)), Encoding.ASCII);
    }
}
using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Zafiro.FileSystem.Lightweight;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarEntryMixin
{
    public static IByteProvider ToByteProvider(this FileTarEntry entry)
    {
        return new CompositeByteProvider(entry.Header(entry.Content.Length, 0), entry.Content);
    }

    public static IByteProvider ToByteProvider(this DirectoryTarEntry entry)
    {
        return new CompositeByteProvider(entry.Header(0, 5).PadToNearestMultiple(512));
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <returns></returns>
    public static IByteProvider Filename(this TarEntry entry) => new StringByteProvider(entry.Path.ToString().Truncate(100).PadRight(100, '\0'), Encoding.ASCII);

    /// <summary>
    ///     From 100 to 108
    /// </summary>
    /// <returns></returns>
    public static IByteProvider FileMode(this TarEntry entry) => new StringByteProvider(entry.Properties.FileMode.ToFileModeString().NullTerminatedPaddedField(8), Encoding.ASCII);

    /// <summary>
    ///     From 108 to 116
    /// </summary>
    public static IByteProvider Owner(this TarEntry entry) => new StringByteProvider(entry.Properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8), Encoding.ASCII);


    /// <summary>
    ///     From 116 to 124
    /// </summary>
    public static IByteProvider Group(this TarEntry entry) => new StringByteProvider(entry.Properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8), Encoding.ASCII);

    public static IByteProvider Header(this TarEntry entry, long fileLength, int linkIndicator)
    {
        var header = entry.HeaderCore(Maybe<long>.None, fileLength, linkIndicator);
        var checkSum = header.Bytes.Flatten().ToEnumerable().Sum(b => (long)b);
        return entry.HeaderCore(checkSum, fileLength, linkIndicator);
    }

    private static IByteProvider HeaderCore(this TarEntry entry, Maybe<long> checkSum, long fileLength, int linkIndicator) => new CompositeByteProvider
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
    private static IByteProvider FileSize(long fileLength) => new StringByteProvider(fileLength.ToOctalField(), Encoding.ASCII);

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private static IByteProvider LastModification(this TarEntry entry) => new StringByteProvider(entry.Properties.LastModification.ToUnixTimeSeconds().ToOctalField(), Encoding.ASCII);

    private static IByteProvider Checksum(Maybe<long> checksum)
    {
        var content = checksum.Match(l => l.ToOctal().PadLeft(6, '0').NullTerminated() + " ", () => new string(' ', 8));
        return new StringByteProvider(content, Encoding.ASCII);
    }

    /// <summary>
    ///     From 157 to 257 Link indicator (file type)
    /// </summary>
    private static IByteProvider NameOfLinkedFile(this TarEntry entry) => new StringByteProvider(new string('\0', 100), Encoding.ASCII);

    /// <summary>
    ///     From 156 to 157 Link indicator (file type)
    /// </summary>
    private static IByteProvider LinkIndicator(int linkIndicator) => new StringByteProvider(linkIndicator.ToString(), Encoding.ASCII);

    private static IByteProvider Ustar() => new StringByteProvider("ustar".PadRight(6, ' '), Encoding.ASCII);

    private static IByteProvider UstarVersion() => new ByteArrayByteProvider([0x20, 0x0]);

    private static IByteProvider OwnerUsername(this TarEntry entry)
    {
        return new StringByteProvider(entry.Properties.OwnerUsername.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32)), Encoding.ASCII);
    }

    private static IByteProvider GroupUsername(this TarEntry entry)
    {
        return new StringByteProvider(entry.Properties.GroupName.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32)), Encoding.ASCII);
    }
}
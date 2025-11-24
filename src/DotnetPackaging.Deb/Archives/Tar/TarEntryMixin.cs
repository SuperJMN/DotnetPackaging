using System.Reactive.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using DotnetPackaging.Deb.Bytes;
using DotnetPackaging.Deb.Unix;
using Zafiro.Reactive;
using Zafiro.Mixins;

namespace DotnetPackaging.Deb.Archives.Tar;

public static class TarEntryMixin
{
    public static IData ToData(this FileTarEntry entry)
    {
        return new CompositeData(entry.Header(entry.Content.Length, 0).PadToNearestMultiple(512), entry.Content.PadToNearestMultiple(512));
    }

    public static IData ToData(this DirectoryTarEntry entry)
    {
        return new CompositeData(entry.Header(0, 5).PadToNearestMultiple(512));
    }

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    /// <returns></returns>
    public static IData Filename(this TarEntry entry) => Data.FromString(entry.Path.ToString().Truncate(100).PadRight(100, '\0'), Encoding.ASCII);

    /// <summary>
    ///     From 100 to 108
    /// </summary>
    /// <returns></returns>
    public static IData FileMode(this TarEntry entry) => Data.FromString(entry.Properties.FileMode.ToFileModeString().NullTerminatedPaddedField(8), Encoding.ASCII);

    /// <summary>
    ///     From 108 to 116
    /// </summary>
    public static IData Owner(this TarEntry entry) => Data.FromString(entry.Properties.OwnerId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8), Encoding.ASCII);

    /// <summary>
    ///     From 116 to 124
    /// </summary>
    public static IData Group(this TarEntry entry) => Data.FromString(entry.Properties.GroupId.GetValueOrDefault(1).ToOctal().NullTerminatedPaddedField(8), Encoding.ASCII);

    public static IData Header(this TarEntry entry, long fileLength, int linkIndicator)
    {
        var header = entry.HeaderCore(Maybe<long>.None, fileLength, linkIndicator);
        var checkSum = header.Bytes.Flatten().ToEnumerable().Sum(b => (long)b);
        return entry.HeaderCore(checkSum, fileLength, linkIndicator);
    }

    private static IData HeaderCore(this TarEntry entry, Maybe<long> checkSum, long fileLength, int linkIndicator) => new CompositeData
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
    private static IData FileSize(long fileLength) => Data.FromString(fileLength.ToOctalField(), Encoding.ASCII);

    /// <summary>
    ///     From 136 to 148 Last modification time in numeric Unix time format (octal)
    /// </summary>
    private static IData LastModification(this TarEntry entry) => Data.FromString(CoerceLastModification(entry.Properties.LastModification).ToOctalField(), Encoding.ASCII);

    private static long CoerceLastModification(DateTimeOffset propertiesLastModification)
    {
        return Math.Max(0, propertiesLastModification.ToUnixTimeSeconds());
    }

    private static IData Checksum(Maybe<long> checksum)
    {
        var content = checksum.Match(l => l.ToOctal().PadLeft(6, '0').NullTerminated() + " ", () => new string(' ', 8));
        return Data.FromString(content, Encoding.ASCII);
    }

    /// <summary>
    ///     From 157 to 257 Link indicator (file type)
    /// </summary>
    private static IData NameOfLinkedFile(this TarEntry entry) => Data.FromString(new string('\0', 100), Encoding.ASCII);

    /// <summary>
    ///     From 156 to 157 Link indicator (file type)
    /// </summary>
    private static IData LinkIndicator(int linkIndicator) => Data.FromString(linkIndicator.ToString(), Encoding.ASCII);

    private static IData Ustar() => Data.FromString("ustar".PadRight(6, ' '), Encoding.ASCII);

    private static IData UstarVersion() => Data.FromByteArray([0x20, 0x0]);

    private static IData OwnerUsername(this TarEntry entry)
    {
        return Data.FromString(entry.Properties.OwnerUsername.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32)), Encoding.ASCII);
    }

    private static IData GroupUsername(this TarEntry entry)
    {
        return Data.FromString(entry.Properties.GroupName.Match(s => s.PadRight(32, '\0'), () => new string('\0', 32)), Encoding.ASCII);
    }
}

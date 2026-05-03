using DotnetPackaging.Deb.Archives.Ar;
using DotnetPackaging.Deb.Archives.Tar;
using Zafiro.DivineBytes;
using DebArEntry = DotnetPackaging.Deb.Archives.Ar.ArEntry;
using DebTarFile = DotnetPackaging.Deb.Archives.Tar.TarFile;

namespace DotnetPackaging.Deb.Tests;

public class ArchiveKnownLengthTests
{
    [Fact]
    public void Ar_byte_source_exposes_length_when_all_entries_are_known()
    {
        var content = ByteSource.FromBytes([1, 2, 3]).WithLength(3);
        var arFile = new ArFile(new DebArEntry("data.tar", content, Misc.RegularFileProperties()));

        var source = arFile.ToByteSource();

        var length = source.KnownLength();
        length.HasValue.Should().BeTrue();
        length.Value.Should().Be(72);
        var bytes = source.Array();
        bytes.Length.Should().Be(72);
        ArArchiveReader.Read(bytes).Single().Data.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Ustar_byte_source_streams_with_calculated_length_when_file_content_is_known()
    {
        var payload = new byte[] { 4, 5, 6 };
        var tarFile = new DebTarFile(new FileTarEntry(
            "./usr/bin/app",
            ByteSource.FromBytes(payload).WithLength(payload.LongLength),
            Misc.ExecutableFileProperties()));

        var source = tarFile.ToByteSource();

        var length = source.KnownLength();
        length.HasValue.Should().BeTrue();
        length.Value.Should().Be(2048);
        var bytes = source.Array();
        bytes.Length.Should().Be(2048);

        var entry = TarArchiveReader.ReadEntries(bytes).Single();
        entry.Name.Should().Be("usr/bin/app");
        entry.Data.Should().Equal(payload);
    }
}

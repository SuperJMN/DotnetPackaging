using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Zafiro.DivineBytes;
using Path = System.IO.Path;

namespace DotnetPackaging.Dmg.Tests;

public class DmgPackagerTests
{
    [Fact]
    public async Task Pack_should_preserve_length_and_delete_temp_dmg_after_consumption()
    {
        using var tempRoot = new TempDir();
        var existingTempDmgs = SnapshotTempDmgs();
        var container = CreateContainer(ByteSource.FromBytes("exe"u8.ToArray()));
        var metadata = new DmgPackagerMetadata
        {
            VolumeName = Maybe.From("Test App"),
            ExecutableName = Maybe.From("TestApp")
        };

        var result = await new DmgPackager().Pack(container, metadata);
        result.IsSuccess.Should().BeTrue();

        var output = Path.Combine(tempRoot.Path, "TestApp.dmg");
        var write = await result.Value.WriteTo(output);

        result.Value.Length.HasValue.Should().BeTrue();
        result.Value.Length.Value.Should().Be(new FileInfo(output).Length);
        write.IsSuccess.Should().BeTrue();
        NewTempDmgs(existingTempDmgs).Should().BeEmpty();
    }

    [Fact]
    public async Task Pack_should_not_leave_temp_dmg_when_container_write_fails()
    {
        var existingTempDmgs = SnapshotTempDmgs();
        var failingSource = ByteSource.FromByteObservable(Observable.Throw<byte[]>(new IOException("boom")));
        var container = CreateContainer(failingSource);

        var result = await new DmgPackager().Pack(container, new DmgPackagerMetadata());

        result.IsFailure.Should().BeTrue();
        NewTempDmgs(existingTempDmgs).Should().BeEmpty();
    }

    private static IContainer CreateContainer(IByteSource source)
    {
        return new RootContainer(
            new[] { new NamedByteSource("TestApp", source) },
            Enumerable.Empty<INamedContainer>());
    }

    private static HashSet<string> SnapshotTempDmgs()
    {
        return Directory
            .EnumerateFiles(Path.GetTempPath(), "dp-dmg-*.dmg")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> NewTempDmgs(HashSet<string> existing)
    {
        return Directory
            .EnumerateFiles(Path.GetTempPath(), "dp-dmg-*.dmg")
            .Where(path => !existing.Contains(path));
    }
}

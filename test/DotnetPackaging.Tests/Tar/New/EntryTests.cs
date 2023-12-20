using FluentAssertions;
using Zafiro.FileSystem;
using Zafiro.FileSystem.Local;

namespace DotnetPackaging.Tests.Tar.New;

public class EntryTests
{
    private readonly IFileSystemRoot fs = new FileSystemRoot(new ObservableFileSystem(LocalFileSystem.Create()));

    [Fact]
    public async Task Test_entry_data_length()
    {
        (await EntryFactory.Create(fs, "TestFiles/icon.png", "Entry")).Should().Succeed().And.Subject.Value.Length.Should().Be(101376 + 512);
    }
}
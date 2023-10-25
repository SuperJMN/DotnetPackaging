using Archiver;

namespace Archive.Tests;

public class DebFileTests
{
    [Fact]
    public void Create()
    {
        using (var file = File.Create("c:\\users\\jmn\\Desktop\\file.deb"))
        {
            DebFile.Create(file);
        }
    }
}
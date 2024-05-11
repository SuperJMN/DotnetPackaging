using DotnetPackaging.Deb.Builder;
using Xunit;
using Zafiro.DataModel;
using Zafiro.FileSystem.Unix;

namespace DotnetPackaging.Deb.Tests;

public class UnitRootToTarFilesTests
{
    [Fact]
    public void Convert()
    {
        var containerOptionsSetup = new ContainerOptionsSetup();
        
        var entries = TarEntryBuilder.From(new UnixRoot(new List<UnixNode>()
        {
            new UnixFile("File1", (StringData)"Contenido"), 
            new UnixDir("MyDir", new List<UnixNode>()
            {
                new UnixFile("File2", (StringData)"Contenido"), 
            }), 
        })).ToList();
    }
}
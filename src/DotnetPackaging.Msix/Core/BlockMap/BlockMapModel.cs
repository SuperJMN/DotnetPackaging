namespace DotnetPackaging.Msix.Core.BlockMap;

// Clases de modelo
public class BlockMapModel
{
    public string HashMethod { get; }
    public ImmutableList<FileBlockInfo> Files { get; }

    public BlockMapModel(string hashMethod, ImmutableList<FileBlockInfo> files)
    {
        HashMethod = hashMethod;
        Files = files;
    }
}
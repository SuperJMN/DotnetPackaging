using DotnetPackaging.Hfs.BTree;

namespace DotnetPackaging.Hfs.Catalog;

/// <summary>
/// HFS+ Catalog B-Tree.
/// Contains records for all files and folders on the volume.
/// </summary>
public sealed class CatalogBTree
{
    private readonly BTreeFile btree;

    public CatalogBTree(int nodeSize = 4096)
    {
        // Max key length for catalog: keyLength(2) + parentID(4) + nameLength(2) + name(255*2) = 518
        btree = new BTreeFile(nodeSize, maxKeyLength: 518);
    }

    /// <summary>
    /// Gets the underlying B-tree file.
    /// </summary>
    public BTreeFile BTree => btree;

    /// <summary>
    /// Adds a folder record and its thread record to the catalog.
    /// </summary>
    public void AddFolder(CatalogNodeId parentId, string name, CatalogFolderRecord folder)
    {
        // Add the folder record (key: parentID + name)
        var key = CatalogKey.For(parentId, name);
        var keyBytes = key.ToBytes();
        var recordBytes = folder.ToBytes();
        
        var combined = new byte[keyBytes.Length + recordBytes.Length];
        keyBytes.CopyTo(combined, 0);
        recordBytes.CopyTo(combined, keyBytes.Length);
        btree.AddLeafRecord(combined);

        // Add the thread record (key: folderID + "")
        var threadKey = CatalogKey.ForThread(folder.FolderId);
        var threadKeyBytes = threadKey.ToBytes();
        var thread = CatalogThreadRecord.ForFolder(parentId, name);
        var threadBytes = thread.ToBytes();
        
        var threadCombined = new byte[threadKeyBytes.Length + threadBytes.Length];
        threadKeyBytes.CopyTo(threadCombined, 0);
        threadBytes.CopyTo(threadCombined, threadKeyBytes.Length);
        btree.AddLeafRecord(threadCombined);
    }

    /// <summary>
    /// Adds a file record and its thread record to the catalog.
    /// </summary>
    public void AddFile(CatalogNodeId parentId, string name, CatalogFileRecord file)
    {
        // Add the file record
        var key = CatalogKey.For(parentId, name);
        var keyBytes = key.ToBytes();
        var recordBytes = file.ToBytes();
        
        var combined = new byte[keyBytes.Length + recordBytes.Length];
        keyBytes.CopyTo(combined, 0);
        recordBytes.CopyTo(combined, keyBytes.Length);
        btree.AddLeafRecord(combined);

        // Add the thread record
        var threadKey = CatalogKey.ForThread(file.FileId);
        var threadKeyBytes = threadKey.ToBytes();
        var thread = CatalogThreadRecord.ForFile(parentId, name);
        var threadBytes = thread.ToBytes();
        
        var threadCombined = new byte[threadKeyBytes.Length + threadBytes.Length];
        threadKeyBytes.CopyTo(threadCombined, 0);
        threadBytes.CopyTo(threadCombined, threadKeyBytes.Length);
        btree.AddLeafRecord(threadCombined);
    }

    /// <summary>
    /// Adds the root folder record.
    /// </summary>
    public void AddRootFolder(uint valence = 0)
    {
        var folder = new CatalogFolderRecord
        {
            FolderId = CatalogNodeId.RootFolder,
            Valence = valence
        };
        AddFolder(CatalogNodeId.RootParent, string.Empty, folder);
    }

    /// <summary>
    /// Gets the total size in bytes.
    /// </summary>
    public int TotalSize => btree.TotalSize;

    /// <summary>
    /// Serializes the catalog to bytes.
    /// </summary>
    public byte[] ToBytes() => btree.ToBytes();

    /// <summary>
    /// Creates an IByteSource.
    /// </summary>
    public IByteSource ToByteSource() => btree.ToByteSource();
}

using DotnetPackaging.Dmg.Hfs.BTree;

namespace DotnetPackaging.Dmg.Hfs.Catalog;

/// <summary>
/// HFS+ Catalog B-Tree.
/// Contains records for all files and folders on the volume.
/// </summary>
public sealed class CatalogBTree
{
    private BTreeFile btree;
    private readonly int nodeSize;
    private readonly List<(CatalogKey Key, byte[] Record)> pendingRecords = new();
    private bool isBuilt = false;

    public CatalogBTree(int nodeSize = 4096)
    {
        // Max key length for catalog: parentID(4) + nameLength(2) + name(255*2) = 516
        // (Excludes the 2-byte keyLength field itself)
        this.nodeSize = nodeSize;
        btree = new BTreeFile(nodeSize, maxKeyLength: 516);
    }

    private void EnsureBuilt()
    {
        if (!isBuilt)
        {
            // Sort records key
            pendingRecords.Sort((a, b) => CatalogKey.Compare(a.Key, b.Key));

            // Re-create BTree to ensure clean state
            // Catalog requires VariableIndexKeys | BigKeys
            var attributes = BTreeAttributes.BigKeys | BTreeAttributes.VariableIndexKeys;
            btree = new BTreeFile(nodeSize, maxKeyLength: 516, attributes);

            var sortedItems = pendingRecords.Select(x => (x.Key.ToBytes(), x.Record));
            btree.BuildFromSortedRecords(sortedItems);

            isBuilt = true;
        }
    }

    /// <summary>
    /// Gets the underlying B-tree file.
    /// </summary>
    public BTreeFile BTree
    {
        get
        {
            EnsureBuilt();
            return btree;
        }
    }

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
        
        pendingRecords.Add((key, combined));
        isBuilt = false;

        // Add the thread record (key: folderID + "")
        var threadKey = CatalogKey.ForThread(folder.FolderId);
        var threadKeyBytes = threadKey.ToBytes();
        var thread = CatalogThreadRecord.ForFolder(parentId, name);
        var threadBytes = thread.ToBytes();
        
        var threadCombined = new byte[threadKeyBytes.Length + threadBytes.Length];
        threadKeyBytes.CopyTo(threadCombined, 0);
        threadBytes.CopyTo(threadCombined, threadKeyBytes.Length);
        
        pendingRecords.Add((threadKey, threadCombined));
        isBuilt = false;
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
        
        pendingRecords.Add((key, combined));
        isBuilt = false;

        // Add the thread record
        var threadKey = CatalogKey.ForThread(file.FileId);
        var threadKeyBytes = threadKey.ToBytes();
        var thread = CatalogThreadRecord.ForFile(parentId, name);
        var threadBytes = thread.ToBytes();
        
        var threadCombined = new byte[threadKeyBytes.Length + threadBytes.Length];
        threadKeyBytes.CopyTo(threadCombined, 0);
        threadBytes.CopyTo(threadCombined, threadKeyBytes.Length);
        
        pendingRecords.Add((threadKey, threadCombined));
        isBuilt = false;
    }

    /// <summary>
    /// Adds the root folder record.
    /// </summary>
    public void AddRootFolder(string volumeName, uint valence = 0)
    {
        var folder = new CatalogFolderRecord
        {
            FolderId = CatalogNodeId.RootFolder,
            Valence = valence
        };
        
        // 1. Folder Record (Key: Parent=1, Name=VolumeName)
        var parentId = CatalogNodeId.RootParent;
        var folderName = volumeName;
        
        var key = CatalogKey.For(parentId, folderName);
        var keyBytes = key.ToBytes();
        var recordBytes = folder.ToBytes();
        
        var combined = new byte[keyBytes.Length + recordBytes.Length];
        keyBytes.CopyTo(combined, 0);
        recordBytes.CopyTo(combined, keyBytes.Length);
        
        pendingRecords.Add((key, combined));
        isBuilt = false;

        // 2. Thread Record (Key: ID=2, Name="", Data: Parent=1, Name=VolumeName)
        var threadKey = CatalogKey.ForThread(folder.FolderId);
        var threadKeyBytes = threadKey.ToBytes();
        
        // Use volumeName for the thread data, which HFS+ expects for the Root Thread
        var thread = CatalogThreadRecord.ForFolder(parentId, volumeName);
        var threadBytes = thread.ToBytes();
        
        var threadCombined = new byte[threadKeyBytes.Length + threadBytes.Length];
        threadKeyBytes.CopyTo(threadCombined, 0);
        threadBytes.CopyTo(threadCombined, threadKeyBytes.Length);
        
        pendingRecords.Add((threadKey, threadCombined));
        isBuilt = false;
    }

    /// <summary>
    /// Gets the total size in bytes.
    /// </summary>
    public int TotalSize
    {
        get
        {
            EnsureBuilt();
            return btree.TotalSize;
        }
    }

    /// <summary>
    /// Serializes the catalog to bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        EnsureBuilt();
        return btree.ToBytes();
    }

    /// <summary>
    /// Creates an IByteSource.
    /// </summary>
    public IByteSource ToByteSource()
    {
        EnsureBuilt();
        return btree.ToByteSource();
    }
}

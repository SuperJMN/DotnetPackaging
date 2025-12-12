namespace DotnetPackaging.Dmg.Hfs.Catalog;

/// <summary>
/// Catalog Node ID (CNID) - unique identifier for files and folders.
/// </summary>
public readonly record struct CatalogNodeId(uint Value)
{
    /// <summary>Reserved CNID: Parent of root folder.</summary>
    public static readonly CatalogNodeId RootParent = new(1);
    
    /// <summary>Reserved CNID: Root folder.</summary>
    public static readonly CatalogNodeId RootFolder = new(2);
    
    /// <summary>Reserved CNID: Extents overflow file.</summary>
    public static readonly CatalogNodeId ExtentsFile = new(3);
    
    /// <summary>Reserved CNID: Catalog file.</summary>
    public static readonly CatalogNodeId CatalogFile = new(4);
    
    /// <summary>Reserved CNID: Bad blocks file.</summary>
    public static readonly CatalogNodeId BadBlocksFile = new(5);
    
    /// <summary>Reserved CNID: Allocation file.</summary>
    public static readonly CatalogNodeId AllocationFile = new(6);
    
    /// <summary>Reserved CNID: Startup file.</summary>
    public static readonly CatalogNodeId StartupFile = new(7);
    
    /// <summary>Reserved CNID: Attributes file.</summary>
    public static readonly CatalogNodeId AttributesFile = new(8);

    /// <summary>Reserved CNID: Repair catalog file.</summary>
    public static readonly CatalogNodeId RepairCatalogFile = new(14);

    /// <summary>Reserved CNID: Bogus extent file.</summary>
    public static readonly CatalogNodeId BogusExtentFile = new(15);

    /// <summary>First available CNID for user files/folders.</summary>
    public static readonly CatalogNodeId FirstUserCatalogNodeId = new(16);

    public static implicit operator uint(CatalogNodeId cnid) => cnid.Value;
    public static implicit operator CatalogNodeId(uint value) => new(value);

    public override string ToString() => $"CNID({Value})";
}

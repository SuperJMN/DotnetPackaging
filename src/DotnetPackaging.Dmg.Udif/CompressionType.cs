namespace DotnetPackaging.Formats.Dmg.Udif
{
    /// <summary>
    /// Compression types supported for UDIF DMG images
    /// </summary>
    public enum CompressionType : uint
    {
        /// <summary>
        /// UDZO - zlib compression (0x80000005)
        /// </summary>
        Zlib = 0x80000005,
        
        /// <summary>
        /// UDBZ - bzip2 compression (0x80000006)
        /// Better compression ratio than zlib
        /// </summary>
        Bzip2 = 0x80000006
    }
}

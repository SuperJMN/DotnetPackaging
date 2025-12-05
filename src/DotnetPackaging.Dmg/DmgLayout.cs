namespace DotnetPackaging.Dmg;

internal static class DmgLayout
{
    public const int SectorSize = 512;
    public const int DriverDescriptorSectors = 1;
    public const int PartitionMapSectors = 0x3f;
    public const int AtapiSectors = 0x8;
    public const int FreeSectors = 0xa;
    public const int PartitionOffset = DriverDescriptorSectors;
    public const int AtapiOffset = PartitionOffset + PartitionMapSectors;
    public const int UserOffset = AtapiOffset + AtapiSectors;
    public const int ExtraSectors = DriverDescriptorSectors + PartitionMapSectors + AtapiSectors + FreeSectors;
}

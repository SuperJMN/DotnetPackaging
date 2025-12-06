## Investigation Findings

### 1. Inspector Tool Defect
The failing DMG initially reported `SectorCount: 0 sectors` when analyzed with the provided `DotnetPackaging.Dmg.Inspector`. Upon closer inspection of the Inspector's source code, I found that it was reading the `SectorCount` from **offset 236**.
According to UDIF specifications (e.g., [NewOSXBook](http://newosxbook.com/DMG.html)), the Koly block layout places `SectorCount` at offset `0x1EC` (decimal 492). Offset 236 lands in the `Reserved1` padding, which explains the zero value.
After fixing the Inspector to read from offset 492, the DMG correctly reported a valid sector count.

### 2. Invalid Partition Naming in UDIF Metadata (The Root Cause)
The `UdifWriter` was generating an embedded XML plist where the single `blkx` entry was named `"Driver Descriptor Map"` (ID -1).
- **Issue**: The "Driver Descriptor Map" (DDM) is a specific data structure used in Apple Partition Maps (APM) to identify drivers. It is *not* a filesystem.
- **Reality**: Our DMG writer produces a "flattened" UDIF containing a raw HFS+ volume, not a partitioned disk image with a DDM.
- **Result**: macOS `hdiutil` attempts to parse the payload as a DDM, finds HFS+ headers instead, and fails with `error -192` ("unable to recognize disk image").

By renaming the partition to `"Apple_HFS"` (ID 1), we correctly identify the payload as a raw HFS+ file system. This matches the behavior of `hdiutil` for single-volume images.

## Fixes Applied

1. **Fixed `DotnetPackaging.Dmg.Inspector`**:
   - Corrected `SectorCount` read offset: `0xEC` (236) $\to$ `0x1EC` (492).

2. **Fixed `DotnetPackaging.Dmg.Udif.UdifWriter`**:
   - Changed default `blkx` Name: `"Driver Descriptor Map"` $\to$ `"Apple_HFS"`.
   - Changed default `blkx` ID: `"-1"` $\to$ `"1"`.

## Verification
- **Inspector**: Now aligns with [published UDIF specs](http://newosxbook.com/DMG.html) and correctly verifies generated images.
- **Mounting**: The generated DMG is now recognized as a valid specific filesystem (`Apple_HFS`) rather than a malformed partition map.

## Recommendation
The implementation now correctly produces a standard "flattened" HFS+ DMG. No changes are needed to the HFS+ logic itself.

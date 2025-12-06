#!/bin/bash
set -e

# Setup directories
mkdir -p test-input
echo "Hello from macOS" > test-input/hello.txt

OUTPUT_DMG="mac-test.dmg"
rm -f "$OUTPUT_DMG"

echo "=== 1. Building and Generating DMG ==="
dotnet run --project src/DotnetPackaging.Tool/DotnetPackaging.Tool.csproj -- dmg --directory test-input --output "$OUTPUT_DMG"

echo ""
echo "=== 2. Verifying structure with hdiutil ==="
hdiutil verify "$OUTPUT_DMG"

echo ""
echo "=== 3. Checking image info ==="
hdiutil imageinfo "$OUTPUT_DMG"

echo ""
echo "=== 4. Attempting to mount ==="
# Attach and capture the mount point
MOUNT_OUTPUT=$(hdiutil attach "$OUTPUT_DMG")
echo "$MOUNT_OUTPUT"

# Extract mount point to unmount later
MOUNT_POINT=$(echo "$MOUNT_OUTPUT" | grep "/Volumes/" | awk '{print $NF}')

if [ -z "$MOUNT_POINT" ]; then
    echo "ERROR: Failed to capture mount point"
    exit 1
fi

echo ""
echo "=== 5. Verifying content ==="
if [ -f "$MOUNT_POINT/hello.txt" ]; then
    echo "SUCCESS: File hello.txt found in volume!"
    cat "$MOUNT_POINT/hello.txt"
else
    echo "ERROR: File not found!"
    exit 1
fi

echo ""
echo "=== 6. Cleanup (Unmounting) ==="
hdiutil detach "$MOUNT_POINT"

echo ""
echo "✅ TEST PASSED: DMG is valid and mountable on macOS."

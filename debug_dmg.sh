#!/bin/bash
set -e

echo "Attaching Output.dmg (no mount)..."
# Capture the device node (e.g. /dev/disk3)
DEVICE=$(hdiutil attach -nomount Output.dmg | grep '/dev/disk' | head -n 1 | awk '{print $1}')

if [ -z "$DEVICE" ]; then
  echo "Error: Failed to attach Output.dmg"
  exit 1
fi

echo "Attached as: $DEVICE"
echo "Running fsck_hfs -d (sudo required)..."

# Run fsck_hfs with debug flag
sudo fsck_hfs -d "$DEVICE" || true

echo ""
echo "Detaching $DEVICE..."
hdiutil detach "$DEVICE"

echo "Done."

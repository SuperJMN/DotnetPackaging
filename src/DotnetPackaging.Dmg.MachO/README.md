# DotnetPackaging.Dmg.MachO

## Status: Work In Progress / Future Use

This project contains utilities for working with Mach-O binaries on macOS.

## Purpose

- **Code signing**: Sign .app bundles and executables for distribution on macOS
- **Binary inspection**: Read and validate Mach-O headers and load commands
- **Entitlements**: Manage app entitlements and capabilities

## Current State

Contains skeleton implementation:
- `CodeSigner.cs`: Basic code signing infrastructure
- `MachOTypes.cs`: Mach-O binary format structures

## Future Work

To make this production-ready:

1. Implement complete Mach-O parsing (headers, load commands, segments)
2. Add codesign wrapper or native signing implementation
3. Support for entitlements.plist embedding
4. Notarization workflow integration
5. Ad-hoc signing for local development/testing

## Why Not Implemented Yet?

DMG creation works without code signing for development/testing. Signing is only required for:
- App Store distribution
- Notarization (required for Gatekeeper on macOS 10.15+)
- Enterprise distribution

The current focus is on producing valid DMG containers with proper HFS+ and UDIF formatting.
Code signing can be added as a post-processing step when needed.

## References

- [Apple Code Signing Guide](https://developer.apple.com/library/archive/documentation/Security/Conceptual/CodeSigningGuide/)
- [Mach-O File Format](https://github.com/aidansteele/osx-abi-macho-file-format-reference)
- [codesign man page](https://www.manpagez.com/man/1/codesign/)

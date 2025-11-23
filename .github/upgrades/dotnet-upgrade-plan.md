# .NET 10.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10.0 upgrade.
3. Upgrade src\DotnetPackaging\DotnetPackaging.csproj
4. Upgrade src\DotnetPackaging.Deb\DotnetPackaging.Deb.csproj
5. Upgrade src\DotnetPackaging.Exe\DotnetPackaging.Exe.csproj
6. Upgrade src\DotnetPackaging.Exe.Installer\DotnetPackaging.Exe.Installer.csproj
7. Upgrade src\DotnetPackaging.Msix\DotnetPackaging.Msix.csproj
8. Upgrade src\DotnetPackaging.Dmg\DotnetPackaging.Dmg.csproj
9. Upgrade src\DotnetPackaging.AppImage\DotnetPackaging.AppImage.csproj
10. Upgrade src\DotnetPackaging.Rpm\DotnetPackaging.Rpm.csproj
11. Upgrade src\DotnetPackaging.Flatpak\DotnetPackaging.Flatpak.csproj
12. Upgrade test\DotnetPackaging.Exe.Tests\DotnetPackaging.Exe.Tests.csproj
13. Upgrade test\DotnetPackaging.Msix.Tests\DotnetPackaging.Msix.Tests.csproj
14. Upgrade test\DotnetPackaging.Dmg.Tests\DotnetPackaging.Dmg.Tests.csproj
15. Upgrade test\DotnetPackaging.AppImage.Tests\DotnetPackaging.AppImage.Tests.csproj
16. Upgrade test\DotnetPackaging.Flatpak.Tests\DotnetPackaging.Flatpak.Tests.csproj
17. Upgrade src\DotnetPackaging.Tool\DotnetPackaging.Tool.csproj

## Settings

This section contains settings and data used by execution steps.

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### src\DotnetPackaging\DotnetPackaging.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.Deb\DotnetPackaging.Deb.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.Exe\DotnetPackaging.Exe.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.Exe.Installer\DotnetPackaging.Exe.Installer.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net10.0-windows`

#### src\DotnetPackaging.Msix\DotnetPackaging.Msix.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.Dmg\DotnetPackaging.Dmg.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.AppImage\DotnetPackaging.AppImage.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.Rpm\DotnetPackaging.Rpm.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### src\DotnetPackaging.Flatpak\DotnetPackaging.Flatpak.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### test\DotnetPackaging.Exe.Tests\DotnetPackaging.Exe.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0-windows` to `net10.0-windows`

#### test\DotnetPackaging.Msix.Tests\DotnetPackaging.Msix.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### test\DotnetPackaging.Dmg.Tests\DotnetPackaging.Dmg.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

#### test\DotnetPackaging.AppImage.Tests\DotnetPackaging.AppImage.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

#### test\DotnetPackaging.Flatpak.Tests\DotnetPackaging.Flatpak.Tests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

#### src\DotnetPackaging.Tool\DotnetPackaging.Tool.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

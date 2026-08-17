# Sanctuary Linux Installer v0.2.9-alpha

This alpha incorporates the latest real-machine Linux Mint testing feedback and finishes another focused installer polish pass.

## Highlights

- Increases the fixed installer window height slightly while preserving the current width and non-resizable layout.
- Rewords the graphics requirement to **Working graphics drivers with 32-bit Vulkan support**.
- Deduplicates Proton builds discovered through multiple Steam path aliases by resolving canonical filesystem paths before display.
- Keeps Proton Experimental, Proton Hotfix, GE-Proton, and other detected compatibility tools selectable without duplicate entries.
- Adds concise Step 3 instructions for installing additional official Proton versions through the Steam Library's Tools filter.
- Adds an explicit **I accept these installation settings and want to continue** checkbox to the Summary panel; Next remains disabled until accepted.
- Reapplies the bundled Sanctuary window icon during the installation state transition to address the installer taskbar icon disappearing when Install is clicked.
- Preserves the working installed Sanctuary/OSFR launcher icon-theme integration and taskbar grouping.
- Preserves custom install locations, dedicated Proton prefixes, safe uninstall boundaries, staged extraction, symlink protections, diagnostics, dry-run support, log rotation, and checksum verification.
- Continues running NuGet vulnerability scanning, installer/launcher safety tests, desktop-entry validation, packaged installer smoke tests, SHA-256 generation, and release-time checksum verification.

## Verify the download

Download both release files into the same directory and run:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
```

A successful verification reports `Sanctuary-Linux-Installer: OK`.

## Requirements

- x86_64 / AMD64 Linux
- Graphical Linux desktop
- Steam installed and working
- An installed Proton build
- Working graphics drivers with 32-bit Vulkan support
- Internet connection and enough disk space for downloaded Open Source Free Realms clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This is still a prerelease. Additional real-machine testing on other Linux distributions, Flatpak Steam installations, custom Steam libraries, SteamOS, and varied GPU/driver combinations is encouraged before a stable release.

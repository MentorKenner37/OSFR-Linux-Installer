# Sanctuary Linux Installer v0.2.8-alpha

This alpha finishes the current installer polish pass and improves Linux desktop integration for a more consistent installed-app experience.

## Highlights

- Locks the installer window to its finalized size and disables resizing.
- Moves **Close** to the lower-left and places **Back** beside **Next / Install**.
- Reworks **Installation Requirements** into a cleaner scrollable list.
- Moves **Close Sanctuary Linux Installer after a successful installation** to the fifth Install panel.
- Expands Proton discovery so more installed Proton/GE-Proton/custom compatibility-tool builds are exposed in the selector.
- Keeps the recommended Proton build selected automatically while allowing manual override.
- Improves Linux icon integration by installing `osfr-linux.png` into the user's hicolor icon theme.
- Uses `Icon=osfr-linux` in generated desktop entries for stable menu/desktop icon resolution.
- Adds `StartupWMClass=OSFRLauncher` and `X-GNOME-WMClass=OSFRLauncher` to improve launcher/taskbar grouping.
- Refreshes desktop and icon caches when the relevant Linux utilities are available.
- Removes the installed icon during uninstall and refreshes desktop integration afterward.
- Keeps the installer window icon explicitly set from the bundled Sanctuary branding resource.
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
- Working graphics drivers with the 32-bit Vulkan support needed by Steam Proton
- Internet connection and enough disk space for downloaded Open Source Free Realms clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This is still a prerelease. Additional real-machine testing on other Linux distributions, Flatpak Steam installations, custom Steam libraries, SteamOS, and varied GPU/driver combinations is encouraged before a stable release.

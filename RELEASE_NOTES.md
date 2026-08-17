# OSFR Linux Installer v0.2.5-alpha

This alpha refreshes the installer interface while preserving the hardened Linux installation flow introduced in earlier releases.

## Highlights

- Redesigned the Avalonia installer with a cleaner graphite/black interface and restrained red gaming accents.
- Added a left-side system-readiness panel and a top visual install-step rail.
- Improved spacing, hierarchy, controls, cards, and primary-action styling without changing the underlying install/uninstall behavior.
- Keeps Proton detection and manual Proton selection, custom install locations, live path validation, safe uninstall boundaries, staged extraction, and symlink protections.
- Retains the v0.2.4 fix that keeps installer logs under `~/.local/state/OSFR-Linux/installer.log` so opening the installer does not occupy the default install directory.
- Keeps NuGet vulnerability scanning, installer/launcher safety tests, desktop-entry validation, packaged installer smoke tests, SHA-256 generation, and release-time checksum verification.

## Verify the download

Download both release files into the same directory and run:

```bash
sha256sum -c OSFR-Linux-Installer.sha256
```

A successful verification reports `OSFR-Linux-Installer: OK`.

## Requirements

- x86_64 / AMD64 Linux
- Graphical Linux desktop
- Steam installed and working
- An installed Proton build
- Working graphics drivers with the 32-bit Vulkan support needed by Steam Proton
- Internet connection and enough disk space for downloaded OSFR clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This is still a prerelease. Additional real-machine testing on other Linux distributions, Flatpak Steam installations, custom Steam libraries, SteamOS, and varied GPU/driver combinations is encouraged before a stable release.

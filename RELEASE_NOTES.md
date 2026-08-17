# OSFR Linux Installer v0.2.6-alpha

This alpha turns the redesigned installer into a true five-step setup flow and introduces the official OSFR Linux Installer icon throughout the application.

## Highlights

- Reworked the installer into a real five-step wizard: Welcome/System Check, Install Location, Proton, Summary, and Install.
- Added working Back/Next navigation and step validation so users cannot advance past unmet requirements or invalid paths.
- Preserves the user's manually selected Proton version when installation starts.
- Added the official OSFR icon as the single branding asset used by the installer.
- Uses the official icon inside the installer UI and as the installer window/taskbar icon.
- Installs the same official icon for the OSFR application-menu and desktop shortcuts.
- Removed the obsolete split/base64 icon assets and updated CI to validate the real PNG asset directly.
- Retains the hardened install/uninstall flow, staged ZIP extraction, symlink protections, dedicated Proton prefix, safe path validation, and installer logging under `~/.local/state/OSFR-Linux`.
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

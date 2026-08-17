# OSFR Linux Installer v0.2.4-alpha

This alpha fixes a default-install-path conflict discovered during real-machine testing on Linux Mint.

## Highlights

- Moved installer diagnostics from `~/.local/share/OSFR-Linux/installer.log` to `~/.local/state/OSFR-Linux/installer.log`.
- Keeps the default installation directory at `~/.local/share/OSFR-Linux` without the installer recreating or occupying it just by launching.
- Preserves log rotation, checksum verification, dependency vulnerability scanning, packaged installer smoke tests, desktop-entry validation, and all v0.2.2/v0.2.3 security hardening.

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
- Up-to-date graphics drivers with the 32-bit Vulkan support needed by Steam Proton
- Internet connection and enough disk space for downloaded OSFR clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This is still a prerelease. Additional real-machine testing on other Linux distributions, Flatpak Steam installations, custom Steam libraries, SteamOS, and varied GPU/driver combinations is encouraged before a stable release.

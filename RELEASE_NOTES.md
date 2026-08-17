# Sanctuary Linux Installer v0.2.11-alpha

This alpha corrects the window behavior and spacing regression introduced by the previous UI cleanup.

## Highlights

- Restores the normal Linux system title bar so the installer can be dragged around naturally again.
- Keeps the **ALPHA** badge in the left branding area.
- Keeps the redundant in-app header/subtitle removed.
- Increases the fixed window height slightly from 790 to 825 pixels so the bottom controls and content are no longer clipped while retaining the 1180-pixel width and non-resizable layout.
- Preserves the **SANCTUARY / LINUX INSTALLER** branding under the icon and the **Welcome to Sanctuary Linux Installer** Step 1 heading.
- Preserves the corrected Steam Proton instructions, Proton deduplication, Summary acceptance checkbox, taskbar icon behavior, custom install locations, dedicated prefixes, safety checks, diagnostics, and checksum verification.

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

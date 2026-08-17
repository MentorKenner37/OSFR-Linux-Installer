# Sanctuary Linux Installer v0.2.10-alpha

This alpha completes the latest installer UI and branding polish pass from real-machine Linux Mint testing.

## Highlights

- Removes the redundant top installer title/header strip while retaining the internal window title for product identity.
- Keeps the **SANCTUARY / LINUX INSTALLER** branding underneath the icon and the **Welcome to Sanctuary Linux Installer** Step 1 heading.
- Relocates the **ALPHA** badge into the left branding area and moves the step navigation/main content upward.
- Removes the redundant **Configure Open Source Free Realms for Steam Proton** subtitle.
- Corrects Step 3 Proton help text for the current Steam UI: users are instructed to search for Proton in their Steam Library rather than use a Tools filter.
- Retains the fixed 1180×790 installer size and the working taskbar icon behavior confirmed in v0.2.9-alpha testing.
- Retains canonical-path Proton deduplication and the Summary acceptance checkbox.
- Standardizes user-facing product branding on **Sanctuary Linux Installer** while intentionally retaining legacy OSFR internal paths and identifiers where changing them could break compatibility.
- Preserves custom install locations, dedicated Proton prefixes, safe uninstall boundaries, staged extraction, symlink protections, diagnostics, dry-run support, log rotation, desktop integration, and checksum verification.

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

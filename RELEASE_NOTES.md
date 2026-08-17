# Sanctuary Linux Installer v0.2.7-alpha

This alpha rebrands the Linux installer as Sanctuary Linux Installer and refreshes the interface with a green visual identity and more professional setup language.

## Highlights

- Rebranded the installer UI, window title, product metadata, diagnostics, and release artifacts as Sanctuary Linux Installer.
- Replaced the previous red accent color with a consistent green accent across buttons, progress indicators, focus states, step navigation, headings, and status elements.
- Renamed the system status area to **System Compatibility** and clarified the detected requirements.
- Reworked the introductory requirements panel with more professional language explaining the supported environment and what the installer does and does not modify.
- Retains the real five-step setup flow: Welcome, Location, Proton, Summary, and Install.
- Preserves manual Proton selection, custom install locations, dedicated Proton prefixes, safe uninstall boundaries, staged extraction, symlink protections, and path validation.
- Continues using the official installer icon in the application window, installer UI, application menu, and desktop shortcut.
- Keeps internal OSFR data paths and launcher identifiers for compatibility with existing installations while presenting Sanctuary branding to users.
- Keeps NuGet vulnerability scanning, installer/launcher safety tests, desktop-entry validation, packaged installer smoke tests, SHA-256 generation, and release-time checksum verification.

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

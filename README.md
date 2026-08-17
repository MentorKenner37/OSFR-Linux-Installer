# Open Source Free Realms - Linux Installer

A self-contained C# Linux installer for Open Source Free Realms using Steam Proton.

## End-user requirements

- x86_64 / AMD64 Linux
- Steam
- An installed Proton build

The packaged GitHub Actions build is self-contained. End users do **not** need Python, Bash installer scripts, or the .NET SDK to run it.

## What it does

- Detects Linux and CPU architecture
- Detects native and Flatpak Steam installations
- Detects Proton Experimental, standard Proton releases, and compatibility tools such as GE-Proton
- Detects additional Steam library folders
- Installs the patched native Linux OSFR Launcher from an embedded payload
- Creates a dedicated Proton prefix
- Records the exact Steam and Proton paths for the launcher
- Verifies the Linux x64 Skia runtime
- Creates desktop integration
- Launches OSFR after installation
- Completely uninstalls the launcher, Proton prefix, downloaded server clients, OSFR data, and shortcuts

## Project structure

- `src/Installer/` - pure C# Avalonia Linux installer
- `OSFR-Launcher/` - modified C# OSFR Launcher source
- `.github/workflows/build-linux-installer.yml` - builds the launcher payload and self-contained installer

## Building

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

The release pipeline publishes the patched launcher for `linux-x64`, embeds it into the installer, then publishes the installer as a self-contained single-file application.

## Distribution

Download the `OSFR-Linux-Installer-linux-x64` artifact produced by the **Build Linux Installer** GitHub Actions workflow.

The intended user flow is:

1. Download `OSFR-Linux-Installer`.
2. Make it executable if the browser removed the executable bit.
3. Run it.
4. The installer detects Steam and Proton.
5. Click **Install**.
6. The OSFR Launcher opens and downloads the selected server clients normally.

## Status

Early development and cross-distribution testing.

The original implementation was tested on Linux Mint. The installer has now been redesigned to avoid distro-specific package-manager logic and machine-specific paths. Additional real-machine testing on Ubuntu, Fedora, Arch-based distributions, Steam Flatpak, and custom Steam libraries is still recommended before declaring a stable release.

## Credits

Based on the Open Source Free Realms Launcher project.
See `OSFR-Launcher/LICENSE` for the upstream launcher license.

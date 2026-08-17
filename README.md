# Open Source Free Realms - Linux Installer

A self-contained C# Linux installer for Open Source Free Realms using Steam Proton.

## End-user requirements

- x86_64 / AMD64 Linux
- A graphical Linux desktop session
- Steam installed and able to launch normally
- An installed Proton build
- Up-to-date graphics drivers with the 32-bit Vulkan support needed by Steam Proton
- An internet connection for OSFR login, server data, and client downloads
- Enough free disk space for the launcher, Proton prefix, and downloaded OSFR server clients
- Permission to write to your home directory and chosen installation location

Linux Mint users should normally already have the required Steam and graphics components when Steam and Proton are working correctly.

Release builds are self-contained. End users do **not** need Python, Bash installer scripts, or the .NET SDK.

The installer does **not** install, update, or modify Steam, Proton, or graphics drivers.

## What it does

- Detects Linux and x86_64 architecture
- Detects native and Flatpak Steam installations
- Detects Proton Experimental, standard Proton releases, and compatibility tools such as GE-Proton
- Installs the patched native Linux OSFR Launcher from an embedded payload
- Creates a dedicated Proton prefix
- Records the exact Steam and Proton paths used by the launcher
- Verifies the Linux x64 Skia runtime
- Creates desktop and application-menu integration
- Launches the OSFR Launcher after installation
- Completely uninstalls the installer-owned launcher, Proton prefix, downloaded server clients, OSFR data, caches, and shortcuts
- Refuses to recursively delete an installation folder unless it can verify that the folder belongs to OSFR

## Downloading

For normal use, download the latest prerelease or release from this repository's **Releases** page. The downloadable file is named `OSFR-Linux-Installer`.

GitHub Actions also produces an `OSFR-Linux-Installer-linux-x64` workflow artifact for development and CI testing. That artifact is not the preferred end-user download when a GitHub Release is available.

## Running

1. Download `OSFR-Linux-Installer` from Releases.
2. If your browser removed the executable bit, run `chmod +x OSFR-Linux-Installer`.
3. Run `./OSFR-Linux-Installer`.
4. Confirm that Linux, x86_64, Steam, and Proton are detected.
5. Choose a dedicated install folder and click **Install**.
6. The OSFR Launcher opens and downloads each selected server's client files normally.

The installer will reject a non-empty unrelated folder rather than risk overwriting or deleting user data.

## Project structure

- `src/Installer/` - C# Avalonia installer
- `src/Installer.SmokeTests/` - dependency-free C# safety smoke tests
- `OSFR-Launcher/` - modified C# OSFR Launcher source
- `Directory.Build.props` - shared installer/launcher version metadata
- `.github/workflows/build-linux-installer.yml` - CI, packaging, artifact, and release pipeline

## Building

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

The pipeline runs installer safety smoke tests, publishes the patched launcher for `linux-x64`, embeds that launcher into the installer, and publishes the installer as a self-contained single-file Linux application.

## Compatibility status

Linux Mint x86_64 with a working Steam + Proton setup is the primary tested environment.

The code also includes detection for Flatpak Steam, custom Steam library folders, Proton Experimental, standard Proton releases, and GE-Proton. Those paths are supported by the implementation but should continue to receive real-machine testing across Ubuntu, Fedora, Arch-based distributions, different desktop environments, and different GPU/driver combinations before the project is described as universally compatible.

## Versioning and releases

The installer and launcher share one version from `Directory.Build.props`. CI builds every push and pull request. A new GitHub prerelease is created only when the shared version does not already have a matching release tag.

## Credits

Based on the Open Source Free Realms Launcher project.
See `OSFR-Launcher/LICENSE` for the upstream launcher license.

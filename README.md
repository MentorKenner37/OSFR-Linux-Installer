# Sanctuary Linux Launcher

Sanctuary Linux Launcher is a Linux-focused installer and launcher setup for **Open Source Free Realms**, built to run the game through **Steam Proton** without requiring users to manually configure Wine prefixes or launcher paths.

> **Status:** Alpha 1. Linux Mint x86_64 is the primary tested platform while broader distro testing continues.

## What it does

- Detects native and Flatpak Steam installations
- Detects Proton Experimental, standard Proton releases, and GE-Proton builds
- Verifies Proton architecture compatibility before use
- Lets you choose which detected Proton version Sanctuary should use
- Installs the patched Open Source Free Realms launcher
- Creates and manages a dedicated Proton prefix for Free Realms
- Creates desktop and application-menu shortcuts
- Supports safe upgrades, rollback, interrupted-install recovery, and uninstall
- Validates install paths, symlinks, ownership metadata, archive paths, and launcher integrity
- Writes diagnostics to `~/.local/state/OSFR-Linux/installer.log`

Sanctuary does **not** install, update, or modify Steam, Proton, graphics drivers, Linux users, system services, or package-manager configuration.

## Requirements

- x86_64 / AMD64 Linux
- Graphical desktop environment
- Steam installed and working
- At least one compatible Proton version installed
- Working graphics drivers with 32-bit Vulkan support
- Internet connection
- Enough disk space for the launcher, Proton prefix, and downloaded game files

Release builds are self-contained. End users do **not** need Python, build scripts, or the .NET SDK.

## Install

Download these two files from the latest GitHub release:

```text
Sanctuary-Linux-Installer
Sanctuary-Linux-Installer.sha256
```

Verify the installer:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
```

Make it executable and launch it:

```bash
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

Inside the installer, confirm Steam and Proton detection, choose an install location and Proton version, review the summary, accept the installation settings, and install.

To install another official Proton version, search for **Proton** in your Steam Library, install the version you want, then restart Sanctuary Linux Installer.

## Diagnostics

Run a read-only compatibility check:

```bash
./Sanctuary-Linux-Installer --diagnose
```

Preview the default installation plan without changing files:

```bash
./Sanctuary-Linux-Installer --dry-run
```

Installer logs are stored at:

```text
~/.local/state/OSFR-Linux/installer.log
```

## Safety and reliability

Sanctuary uses staged installation, transactional launcher replacement, rollback, crash recovery, structured ownership metadata, launcher SHA-256 verification, archive traversal protection, symlink checks, conservative recursive deletion, and dedicated install-state validation.

The repository also contains xUnit regression tests and smoke tests for installation transactions, recovery, ownership rejection, path traversal, symlink handling, install-path validation, Proton runtime architecture detection, launcher path safety, and packaged-installer behavior.

GitHub Actions additionally checks NuGet dependencies for known vulnerabilities, validates the generated `.desktop` entry, builds the patched launcher and self-contained installer, runs packaged diagnostics/dry-run checks, and verifies the published SHA-256 checksum.

## Compatibility

### Tested

- Linux Mint x86_64 with Steam and Proton

### In testing

- Debian
- Ubuntu
- Fedora
- Arch-based distributions
- SteamOS / Steam Deck
- Additional desktop environments, Steam layouts, filesystems, and GPU/driver combinations

Native Steam, Flatpak Steam, custom Steam libraries, standard Proton, Proton Experimental, and GE-Proton detection are supported by the current codebase, but real-machine validation is still expanding during alpha.

## Building from source

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

The main components are:

```text
src/Installer/                  Avalonia Sanctuary installer
src/Installer.SmokeTests/       Installer safety smoke tests
src/Launcher.SmokeTests/        Launcher path-safety smoke tests
tests/Installer.UnitTests/      xUnit regression tests
OSFR-Launcher/                  Patched upstream launcher source
.github/workflows/              Build, test, verification, and release automation
```

End users should use the packaged installer from **Releases** rather than building from source.

## Credits and licensing

Sanctuary uses and adapts the Open Source Free Realms launcher. See `OSFR-Launcher/LICENSE` for the upstream launcher license.

The launcher includes Discord Rich Presence integration using the Discord Game SDK. This project is not created by or endorsed by Discord.

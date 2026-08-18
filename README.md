# Sanctuary Linux Launcher

Sanctuary Linux Launcher is a Linux-focused installer and launcher setup for **Open Source Free Realms**, built to run the game through **Steam Proton** without requiring users to manually configure Wine prefixes or launcher paths.

> **Status:** Alpha 1. Linux Mint and Debian 13 x86_64 are tested and working. Broader distro and hardware testing continues.

## What it does

- Detects native and Flatpak Steam installations
- Detects Proton Experimental, standard Proton releases, and GE-Proton builds
- Verifies Proton architecture compatibility before use
- Lets you choose which detected Proton version Sanctuary should use
- Lets you choose between **DXVK/Vulkan** and **WineD3D/OpenGL** graphics backends
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
- 32-bit Linux runtime support for Free Realms
- Working graphics drivers
- Vulkan support is recommended for DXVK; WineD3D/OpenGL is available as a fallback
- Internet connection
- Enough disk space for the launcher, Proton prefix, and downloaded game files

Release builds are self-contained. End users do **not** need Python, build scripts, or the .NET SDK.

### 32-bit runtime requirements

Free Realms is a 32-bit Windows game. On a fresh 64-bit Linux installation, the required 32-bit runtime and graphics libraries may not be installed by default.

A Debian x86_64 test initially reached Proton prefix startup but failed to display the launcher/game. The first visible symptom was:

```text
Wine cannot find the FreeType font library.
```

After enabling i386 multiarch and installing the required 32-bit FreeType/OpenGL/Mesa libraries, Free Realms successfully launched through Sanctuary on Debian.

For Debian-based systems, the known working package set is:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These packages are system dependencies and are intentionally not bundled into Sanctuary. Future installer diagnostics should detect missing 32-bit runtime components and report the required distro-specific packages without silently modifying the user's system.

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

Inside the installer, confirm Steam and Proton detection, choose an install location, Proton version, and graphics backend, review the summary, accept the installation settings, and install.

To install another official Proton version, search for **Proton** in your Steam Library, install the version you want, then restart Sanctuary Linux Installer.

## Graphics backends

Sanctuary remains Proton-based in both modes:

- **Vulkan (DXVK) — Recommended:** translates Free Realms' Direct3D rendering through DXVK to Vulkan.
- **OpenGL (WineD3D) — Compatibility fallback:** translates Direct3D through WineD3D to OpenGL for systems without usable Vulkan support.

The OpenGL option is intended for older or incomplete Vulkan implementations and does not switch Sanctuary to system Wine.

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

If Proton initializes (`fsync: up and running.`) but nothing visible opens, verify the 32-bit runtime libraries above before changing other system components.

## Safety and reliability

Sanctuary uses staged installation, transactional launcher replacement, rollback, crash recovery, structured ownership metadata, launcher SHA-256 verification, archive traversal protection, symlink checks, conservative recursive deletion, and dedicated install-state validation.

The repository also contains xUnit regression tests and smoke tests for installation transactions, recovery, ownership rejection, path traversal, symlink handling, install-path validation, Proton runtime architecture detection, launcher path safety, graphics-backend selection, and packaged-installer behavior.

GitHub Actions additionally checks NuGet dependencies for known vulnerabilities, validates the generated `.desktop` entry, builds the patched launcher and self-contained installer, runs packaged diagnostics/dry-run checks, and verifies the published SHA-256 checksum.

## Compatibility

### Tested and working

- **Linux Mint x86_64** — Steam + Proton, Free Realms launches and plays normally
- **Debian 13 x86_64** — Steam + Proton, Free Realms launches and plays normally after installing the required 32-bit runtime libraries

Debian 13 has been validated on more than one machine. A second Debian 13 test on hardware already known to run Sanctuary successfully under Linux Mint confirmed that Free Realms launches and that in-game mouse/camera controls behave normally.

A separate Debian tester experienced abnormal camera/relative-mouse behavior, but because that issue is not reproducible on the other Debian 13 system it is currently tracked as a machine/session/Proton-specific compatibility issue rather than a Debian-wide problem.

### Known compatibility notes

- Free Realms requires 32-bit userspace/runtime support even on a 64-bit Linux host.
- A missing `libfreetype6:i386` can produce the `Wine cannot find the FreeType font library` warning.
- Missing 32-bit OpenGL/Mesa packages can prevent the 32-bit game stack from launching correctly.
- Proton-version differences may affect legacy input behavior such as mouse capture or in-game camera control on some systems. If rendering works but input does not, testing another installed Proton version is a reasonable compatibility step.
- Wayland/XWayland, desktop compositor behavior, hardware/input differences, and prefix state remain possible machine-specific causes when camera input fails on one system but works on another.
- Systems with incomplete or unavailable Vulkan support can use Sanctuary's WineD3D/OpenGL backend instead of DXVK/Vulkan.

### In testing

- Ubuntu
- Fedora
- Arch-based distributions
- SteamOS / Steam Deck
- Additional desktop environments, Steam layouts, filesystems, input devices, and GPU/driver combinations

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

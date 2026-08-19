# Sanctuary Linux Installer

**A streamlined way to get the Sanctuary Free Realms emulator running on Linux through Proton.**

Sanctuary Linux Installer is a Linux-first installation and compatibility tool for **Sanctuary**, the Open Source Free Realms emulator. The installer finds your Steam and Proton setup, prepares a dedicated Free Realms prefix, installs the patched OSFR launcher used to connect to Sanctuary, and handles the Linux-specific setup needed to get into the game.

> **Alpha 1** — Tested and playing on **Linux Mint**, **Debian 13**, and **Fedora Workstation** x86_64. Desktop/session details matter, so the environments that have been confirmed in-game are documented below.

## What is Sanctuary?

**Sanctuary** is the open-source server emulator at the heart of the Open Source Free Realms project. It recreates the server-side systems needed for the original Free Realms client to connect to and interact with a community-run Free Realms environment.

This repository is **not the Sanctuary emulator itself**. The Sanctuary Linux Installer focuses on the other side of the equation: getting the Windows Free Realms client and OSFR launcher running cleanly on Linux through Steam Proton so Linux players can connect to a server powered by Sanctuary.

- **Sanctuary emulator:** https://github.com/Open-Source-Free-Realms/Sanctuary
- **Open Source Free Realms launcher:** https://github.com/Open-Source-Free-Realms/Launcher

In simple terms: **Sanctuary emulates the Free Realms server, the OSFR launcher gets the client ready to connect, and the Sanctuary Linux Installer makes that Windows client/launcher stack practical to run on Linux through Proton.**

## Why the Linux Installer?

Sanctuary brings Free Realms back through an open-source emulator, but running its Windows game client on Linux introduces another compatibility layer. Proton prefixes, executable paths, environment variables, graphics translation, 32-bit libraries, and distro differences can quickly turn installation into its own project.

The Sanctuary Linux Installer is built to handle that Linux-specific setup while leaving control of Steam, Proton, drivers, and system packages in the user's hands.

The installer can:

- Detect **native Steam** and **Flatpak Steam** installations
- Find **Proton Experimental**, standard Proton releases, and **GE-Proton** builds
- Choose which compatible Proton installation should run the Sanctuary/Free Realms client
- Choose **DXVK/Vulkan** or **WineD3D/OpenGL** for rendering
- Install the patched Open Source Free Realms launcher used with Sanctuary
- Keep the Free Realms client isolated in its own dedicated Proton prefix
- Create desktop and application-menu shortcuts
- Safely upgrade, recover, roll back, or uninstall the Linux installation
- Generate useful diagnostics when something goes wrong

The installer does **not** replace Steam, install graphics drivers, modify Linux users or services, or silently change your package-manager configuration.

## Quick start

You need an **x86_64 / AMD64 Linux system**, a graphical desktop, Steam, at least one compatible Proton version, working graphics drivers, an internet connection, and the 32-bit Linux runtime support required by the Free Realms client.

Release builds are self-contained. You do **not** need Python, the .NET SDK, or the repository source to use the installer.

Download these files from the latest release:

```text
Sanctuary-Linux-Installer
Sanctuary-Linux-Installer.sha256
```

Verify the download:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
```

Then make the installer executable and run it:

```bash
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

The installer will walk you through Steam/Proton detection, installation location, Proton selection, graphics backend, and a final summary before anything is installed.

Need another official Proton release? Install it through your Steam Library, restart the Linux installer, and it should appear as an available option.

## Choose your graphics backend

The Linux setup remains Proton-based regardless of which graphics option you select.

### DXVK / Vulkan — Recommended

The Free Realms client's Direct3D rendering is translated through **DXVK to Vulkan**. This is the primary graphics path and is recommended when your GPU and drivers have usable Vulkan support.

### WineD3D / OpenGL — Compatibility mode

Direct3D is translated through **WineD3D to OpenGL**. This gives older systems and machines with missing or incomplete Vulkan support another route into the game.

Selecting OpenGL does **not** switch the installation to system Wine.

## 32-bit runtime support

The Free Realms client is a **32-bit Windows application**, so a 64-bit Linux installation still needs the appropriate 32-bit userspace and graphics libraries. Some distributions do not install these by default.

During Debian testing, Proton successfully initialized but the launcher/game initially failed to appear. The useful clue was:

```text
Wine cannot find the FreeType font library.
```

After enabling Debian's i386 architecture and installing the required 32-bit FreeType/OpenGL/Mesa libraries, the Sanctuary client launched successfully through Proton.

The currently confirmed Debian package set is:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These are operating-system dependencies, so the Linux installer intentionally does not bundle or silently install them. Better distro-specific prerequisite detection is planned as testing expands.

## Compatibility

### ✅ Tested and working

The following are **real tested environments**, not assumptions based only on distro family. "Working" means the installer and OSFR launcher ran, Free Realms reached the game world, and normal in-game controls were confirmed in the tested session unless a caveat is listed.

| Distribution / environment | Result | Tested path |
| --- | --- | --- |
| **Linux Mint x86_64 — Cinnamon / X11** | ✅ Confirmed working | Steam + Proton; Free Realms launches, enters the world, and controls work normally |
| **Debian 13 x86_64** | ✅ Confirmed working | Steam + Proton; Free Realms launches and Shift-walk works with the required 32-bit runtime libraries installed |
| **Fedora Workstation x86_64 — GNOME Classic / Wayland** | ✅ Confirmed working | Proton 11 + DXVK/Vulkan; installer, OSFR launcher, in-game play, and Shift-walk confirmed |
| **Fedora Workstation x86_64 — Cinnamon / Wayland** | ⚠️ Playable with input caveat | Proton 11 + DXVK/Vulkan launches and plays, but Shift + movement does not trigger walking |

### Desktop/session compatibility note

Desktop environment and display session can matter independently of the Linux distribution.

Current Fedora testing is especially useful: **GNOME Classic on Wayland works correctly with Proton 11**, including Shift-walk, while **Cinnamon on Wayland on the same Fedora installation launches and plays but does not recognize Shift-walk correctly**. Because Wayland and Proton 11 both work in the GNOME Classic test, neither should currently be considered globally incompatible with Free Realms.

For Fedora users who encounter the Shift-walk problem under Cinnamon/Wayland, the currently confirmed workaround is to use **GNOME Classic / Wayland**. Fedora Cinnamon/X11 has not yet been tested, so the exact cause remains under investigation.

Linux Mint has been confirmed working with **Cinnamon / X11**. This also means Cinnamon itself is not known to be generally incompatible; the current problem is specifically associated with the tested Cinnamon/Wayland combination.

Debian 13 has been validated on more than one machine. Testing on hardware previously known to run the Sanctuary client under Linux Mint also confirmed normal in-game mouse and camera behavior on Debian.

One separate Debian machine showed abnormal relative-mouse/camera behavior. Because that problem has not reproduced on the other Debian system, it is being tracked as a **machine/session/Proton-specific compatibility issue**, not a Debian-wide failure.

### 🧪 Next in the test queue

- Arch-based distributions
- Ubuntu
- openSUSE
- SteamOS / Steam Deck
- Fedora Cinnamon / X11
- More GPUs and integrated graphics
- DXVK/Vulkan and WineD3D/OpenGL across different hardware
- Additional desktop environments, Steam layouts, filesystems, and input devices

Support in the installer codebase already includes native Steam, Flatpak Steam, custom Steam libraries, standard Proton, Proton Experimental, and GE-Proton detection. Real-machine validation is continuing throughout Alpha.

## Troubleshooting and diagnostics

The Linux installer keeps its log at:

```text
~/.local/state/OSFR-Linux/installer.log
```

Run a read-only compatibility check with:

```bash
./Sanctuary-Linux-Installer --diagnose
```

Or preview the default installation plan without modifying files:

```bash
./Sanctuary-Linux-Installer --dry-run
```

A few things we've learned from real-machine testing:

- The Free Realms client needs **32-bit runtime support** even on x86_64 Linux.
- Missing 32-bit FreeType can produce `Wine cannot find the FreeType font library`.
- Missing 32-bit OpenGL/Mesa libraries can stop the 32-bit game stack from launching correctly.
- If Proton reaches `fsync: up and running.` but nothing visible opens, check the 32-bit runtime before changing unrelated components.
- Desktop environment and session type can affect old-game input behavior even when the same Proton version is used.
- **Fedora GNOME Classic / Wayland + Proton 11** is currently confirmed working, including Shift-walk.
- **Fedora Cinnamon / Wayland + Proton 11** currently has a Shift-walk input issue.
- **Linux Mint Cinnamon / X11** is confirmed working with normal controls.
- Proton-version differences, compositor behavior, hardware/input differences, and prefix state can also matter when an issue appears on only one system.
- If Vulkan is unavailable or unreliable, use the installer's **WineD3D/OpenGL** backend.

## Built to be safe to test

The goal is to make the Linux side of Sanctuary reliable without throwing files around your home directory and hoping for the best.

The installer includes staged installation, transactional launcher replacement, rollback, interrupted-install recovery, ownership metadata, SHA-256 launcher verification, archive traversal protection, symlink checks, conservative recursive deletion, and install-state validation.

The repository also contains **xUnit regression tests and smoke tests** covering installation transactions, recovery, ownership rejection, archive/path traversal, symlinks, install-path validation, Proton architecture detection, launcher path safety, graphics-backend selection, and packaged-installer behavior.

GitHub Actions checks dependencies for known vulnerabilities, validates generated desktop entries, builds the patched launcher and self-contained installer, runs packaged diagnostics and dry-run checks, and verifies the published SHA-256 checksum.

## Building from source

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

```text
src/Installer/                  Avalonia Sanctuary Linux installer
src/Installer.SmokeTests/       Installer safety smoke tests
src/Launcher.SmokeTests/        Launcher path-safety smoke tests
tests/Installer.UnitTests/      xUnit regression tests
OSFR-Launcher/                  Patched upstream launcher source
.github/workflows/              Build, test, verification, and release automation
```

If you just want to play, use the packaged installer from **Releases** instead of building from source.

## Credits and licensing

This project provides the Linux installation and compatibility layer for **Sanctuary / Open Source Free Realms** and uses an adapted version of the Open Source Free Realms launcher.

Upstream projects:

- Sanctuary emulator: https://github.com/Open-Source-Free-Realms/Sanctuary
- Open Source Free Realms launcher: https://github.com/Open-Source-Free-Realms/Launcher

See `OSFR-Launcher/LICENSE` for the upstream launcher license.

The launcher includes Discord Rich Presence integration through the Discord Game SDK. This project is not created by or endorsed by Discord.

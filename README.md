# Sanctuary Linux Launcher

**Free Realms on Linux, without turning Proton setup into a project of its own.**

Sanctuary Linux Launcher is a Linux-first installer for **Open Source Free Realms**. It finds your Steam and Proton setup, prepares a dedicated Free Realms prefix, installs the patched OSFR launcher, and gives you a straightforward way to launch the game through Proton.

> **Alpha 1** — Tested and playing on **Linux Mint**, **Debian 13**, and **Fedora Workstation** x86_64. More distributions, graphics configurations, and hardware are being tested as development continues.

## Why Sanctuary?

Running an older Windows game on Linux can involve prefixes, compatibility layers, paths, environment variables, graphics translation, and plenty of trial and error. Sanctuary is designed to handle that setup while still leaving control of Steam, Proton, and your system in your hands.

With Sanctuary you can:

- Detect **native Steam** and **Flatpak Steam** installations
- Find **Proton Experimental**, standard Proton releases, and **GE-Proton** builds
- Choose which compatible Proton installation Free Realms should use
- Choose **DXVK/Vulkan** or **WineD3D/OpenGL** for rendering
- Install and launch the patched Open Source Free Realms launcher
- Keep Free Realms isolated in its own dedicated Proton prefix
- Create desktop and application-menu shortcuts
- Safely upgrade, recover, roll back, or uninstall Sanctuary
- Generate useful diagnostics when something goes wrong

Sanctuary does **not** replace Steam, install graphics drivers, modify Linux users or services, or silently change your package-manager configuration.

## Quick start

You need an **x86_64 / AMD64 Linux system**, a graphical desktop, Steam, at least one compatible Proton version, working graphics drivers, an internet connection, and the 32-bit Linux runtime support required by Free Realms.

Release builds are self-contained. You do **not** need Python, the .NET SDK, or the repository source to use Sanctuary.

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

Sanctuary will walk you through Steam/Proton detection, installation location, Proton selection, graphics backend, and a final summary before anything is installed.

Need another official Proton release? Install it through your Steam Library, restart Sanctuary, and it should appear as an available option.

## Choose your graphics backend

Sanctuary stays Proton-based regardless of which graphics option you select.

### DXVK / Vulkan — Recommended

Free Realms' Direct3D rendering is translated through **DXVK to Vulkan**. This is the primary graphics path and is recommended when your GPU and drivers have usable Vulkan support.

### WineD3D / OpenGL — Compatibility mode

Direct3D is translated through **WineD3D to OpenGL**. This gives older systems and machines with missing or incomplete Vulkan support another route into the game.

Selecting OpenGL does **not** switch Sanctuary to system Wine.

## 32-bit runtime support

Free Realms is a **32-bit Windows game**, so a 64-bit Linux installation still needs the appropriate 32-bit userspace and graphics libraries. Some distributions do not install these by default.

During Debian testing, Proton successfully initialized but the launcher/game initially failed to appear. The useful clue was:

```text
Wine cannot find the FreeType font library.
```

After enabling Debian's i386 architecture and installing the required 32-bit FreeType/OpenGL/Mesa libraries, Free Realms launched successfully.

The currently confirmed Debian package set is:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These are operating-system dependencies, so Sanctuary intentionally does not bundle or silently install them. Better distro-specific prerequisite detection is planned as testing expands.

## Compatibility

### ✅ Tested and working

| Distribution | Result | Tested path |
| --- | --- | --- |
| **Linux Mint x86_64** | ✅ Working | Steam + Proton; Free Realms launches and plays normally |
| **Debian 13 x86_64** | ✅ Working | Steam + Proton; launches and plays normally with required 32-bit runtime libraries |
| **Fedora Workstation x86_64** | ✅ Working | Steam + Proton + DXVK/Vulkan; installer, launcher, and in-game play confirmed |

Debian 13 has been validated on more than one machine. Testing on hardware previously known to run Sanctuary under Linux Mint also confirmed normal in-game mouse and camera behavior on Debian.

One separate Debian machine showed abnormal relative-mouse/camera behavior. Because that problem has not reproduced on the other Debian system, it is being tracked as a **machine/session/Proton-specific compatibility issue**, not a Debian-wide failure.

### 🧪 Next in the test queue

- Arch-based distributions
- Ubuntu
- openSUSE
- SteamOS / Steam Deck
- More GPUs and integrated graphics
- DXVK/Vulkan and WineD3D/OpenGL across different hardware
- Additional desktop environments, Steam layouts, filesystems, and input devices

Support in the codebase already includes native Steam, Flatpak Steam, custom Steam libraries, standard Proton, Proton Experimental, and GE-Proton detection. Real-machine validation is continuing throughout Alpha.

## Troubleshooting and diagnostics

Sanctuary keeps an installer log at:

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

- Free Realms needs **32-bit runtime support** even on x86_64 Linux.
- Missing 32-bit FreeType can produce `Wine cannot find the FreeType font library`.
- Missing 32-bit OpenGL/Mesa libraries can stop the 32-bit game stack from launching correctly.
- If Proton reaches `fsync: up and running.` but nothing visible opens, check the 32-bit runtime before changing unrelated components.
- Proton-version differences can affect older input behavior such as relative mouse capture on some machines.
- Wayland/XWayland, compositor behavior, hardware/input differences, and prefix state can also matter when an issue appears on only one system.
- If Vulkan is unavailable or unreliable, try Sanctuary's **WineD3D/OpenGL** backend.

## Built to be safe to test

Sanctuary isn't supposed to get Free Realms running by throwing files around your home directory and hoping for the best.

The installer includes staged installation, transactional launcher replacement, rollback, interrupted-install recovery, ownership metadata, SHA-256 launcher verification, archive traversal protection, symlink checks, conservative recursive deletion, and install-state validation.

The repository also contains **xUnit regression tests and smoke tests** covering installation transactions, recovery, ownership rejection, archive/path traversal, symlinks, install-path validation, Proton architecture detection, launcher path safety, graphics-backend selection, and packaged-installer behavior.

GitHub Actions checks dependencies for known vulnerabilities, validates generated desktop entries, builds the patched launcher and self-contained installer, runs packaged diagnostics and dry-run checks, and verifies the published SHA-256 checksum.

## Building from source

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

```text
src/Installer/                  Avalonia Sanctuary installer
src/Installer.SmokeTests/       Installer safety smoke tests
src/Launcher.SmokeTests/        Launcher path-safety smoke tests
tests/Installer.UnitTests/      xUnit regression tests
OSFR-Launcher/                  Patched upstream launcher source
.github/workflows/              Build, test, verification, and release automation
```

If you just want to play, use the packaged installer from **Releases** instead of building from source.

## Credits and licensing

Sanctuary uses and adapts the **Open Source Free Realms** launcher. See `OSFR-Launcher/LICENSE` for the upstream launcher license.

The launcher includes Discord Rich Presence integration through the Discord Game SDK. This project is not created by or endorsed by Discord.

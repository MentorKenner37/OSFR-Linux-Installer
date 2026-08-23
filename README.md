# Sanctuary Linux Installer

**A streamlined way to get the Sanctuary Free Realms emulator running on Linux through Proton.**

Sanctuary Linux Installer is a Linux-first installation and compatibility tool for **Sanctuary**, the Open Source Free Realms emulator. It finds Steam and Proton, prepares a dedicated Free Realms prefix, installs the patched OSFR launcher used to connect to Sanctuary, and handles the Linux-specific setup needed to get into the game.

> **Version 1.1.0-beta.9** — Beta software tested and playing on **Linux Mint**, **Debian 13**, and **Fedora Workstation** x86_64. Desktop/session details matter, so confirmed environments and caveats are documented below.

## What the installer does

- Detects native and Flatpak Steam locations plus custom Steam libraries
- Finds standard Proton, Proton Experimental, GE-Proton, and other compatible Steam compatibility tools
- Verifies Proton runtime architecture before offering a build
- Prefers the newest compatible **stable Proton** release by default, while leaving Experimental and GE-Proton available as selectable troubleshooting/fallback options
- Detects and reports the actual Linux distribution, kernel, CPU, architecture, installed RAM, GPU, desktop environment, and Wayland/X11 session
- Detects 32-bit FreeType and OpenGL runtime availability
- Detects 64-bit and 32-bit Vulkan loader availability
- Identifies native versus Flatpak Steam in both diagnostics and the graphical installer
- Produces a DXVK/Vulkan versus WineD3D/OpenGL recommendation from the detected 32-bit Vulkan state
- Automatically preselects the recommended graphics backend while still allowing the user to override it
- Detects Cinnamon + Wayland and emits a non-blocking warning for the currently known Shift/modifier input caveat
- Surfaces runtime, graphics, Steam-type, hardware, OS, and compatibility details directly in the graphical installer
- Installs the patched Open Source Free Realms launcher and isolates Free Realms in a dedicated Proton prefix
- Offers Free Realms' native fullscreen startup option or its genuine movable window, always launched directly through Proton
- Preserves the game's own in-game window-size controls without Gamescope, virtual desktops, stretching, or letterboxing wrappers
- Checks the official GitHub releases for stable or beta updates, verifies the published SHA-256, and supports opt-in transactional automatic upgrades
- Always creates application-menu integration and lets the user choose whether to create a desktop shortcut
- Lets the user choose whether Sanctuary launches automatically when installation finishes
- Detects existing installations and replaces the normal wizard with a locked maintenance page
- Provides Launch, Repair/Upgrade, safe Uninstall, shortcut management, folder/log access, and redacted diagnostic export
- Uses ownership validation, staged replacement, rollback, recovery, archive traversal protection, symlink checks, and conservative uninstall behavior
- Stages and verifies every downloaded game file before atomically replacing the installed copy
- Requires HTTPS for server manifests, login, and registration
- Provides `--diagnose` and `--dry-run` troubleshooting modes
- Detects the optional `curl` compatibility fallback through `PATH` and reports its availability in diagnostics

The installer does **not** replace Steam, install graphics drivers, modify Linux users/services, or silently change package-manager configuration.

## Quick start

You need an **x86_64 / AMD64 Linux system**, graphical desktop, Steam, at least one compatible Proton build, working graphics drivers, internet access, and the 32-bit Linux runtime support required by the Free Realms client.

Release builds are self-contained. Python, the .NET SDK, and the repository source are not required.

Download `Sanctuary-Linux-Installer` and `Sanctuary-Linux-Installer.sha256` from the latest release, then:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

The installer walks through environment detection, installation location, Proton selection, graphics backend, review, and installation. Uninstall removes the installer-owned program directory and desktop integration but deliberately preserves shared launcher settings and downloaded game data under `~/.local/share/OSFRLauncher`.

## Hardware and OS detection

The installer no longer reduces a machine to generic `SUPPORTED` / `COMPATIBLE` labels. The graphical compatibility view and diagnostics expose the machine that is actually being tested, including:

```text
Operating system: Fedora Linux 44
Kernel: <detected kernel>
Desktop: GNOME / Cinnamon / ...
Session: wayland / x11
CPU: <detected CPU model>
Architecture: x86_64
Memory: <detected installed RAM>
GPU: <detected graphics adapter(s)>
Steam type: Native Steam / Flatpak Steam
Steam: <detected Steam root>
Proton: <selected stable-first Proton build>
```

Host parsing is separated into deterministic parsers for `/etc/os-release`, `/proc/cpuinfo`, `/proc/meminfo`, and `lspci` graphics output so the detection logic can be regression-tested without depending on the CI runner's actual hardware.

## Proton recommendation policy

When multiple compatible Proton builds are installed, Sanctuary Linux Installer prefers the newest standard stable Proton release first. If no standard stable release is available, it falls back to the newest compatible GE-Proton build, then Proton Experimental, then another compatible candidate.

This is only the default selection. Users can still explicitly choose another detected Proton build from the installer.

## Graphics backends

### DXVK / Vulkan

The Free Realms client's Direct3D rendering is translated through DXVK to Vulkan. This is the primary graphics path when usable 32-bit Vulkan support is present.

### WineD3D / OpenGL

WineD3D translates Direct3D through OpenGL and provides a compatibility route for systems with missing or unreliable Vulkan support. Selecting OpenGL does **not** switch the installation to system Wine; the installation remains Proton-based.

The compatibility advisor checks the 64-bit and 32-bit Vulkan loader state. If the 32-bit Vulkan loader is detected, DXVK/Vulkan is recommended and preselected. If 32-bit Vulkan is known to be missing, WineD3D/OpenGL is recommended and preselected as the safer default. If Vulkan availability cannot be verified, DXVK remains the non-blocking default and OpenGL remains available as a fallback.

## 32-bit runtime support

Free Realms is a **32-bit Windows application**, so a 64-bit Linux installation still needs appropriate 32-bit userspace and graphics libraries.

During Debian testing, Proton initialized but the launcher/game initially failed to appear. The useful clue was:

```text
Wine cannot find the FreeType font library.
```

The confirmed Debian package set is:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These are operating-system dependencies and are not silently installed by Sanctuary Linux Installer. The compatibility advisor probes the installed 32-bit FreeType/OpenGL runtime and provides distro-family package guidance when required components are known to be missing. Its `ldconfig` parsing and ELF32/ELF64 library-class checks also have deterministic regression coverage.

## Compatibility

### Known beta issues

- Fedora Cinnamon on Wayland can lose Shift+movement walking behavior; Fedora GNOME Classic on Wayland is confirmed working.
- One Debian tester experienced machine-specific extreme camera/relative-mouse behavior that was not reproduced on a second Debian 13 system.
- `curl` is an optional verified fallback for servers that return a different body to the .NET HTTP client; ordinary downloads do not require it.

| Distribution / environment | Result | Tested path |
| --- | --- | --- |
| **Linux Mint x86_64 — Cinnamon / X11** | ✅ Confirmed working | Steam + Proton; launches, enters the world, controls work normally |
| **Debian 13 x86_64** | ✅ Confirmed working | Steam + Proton; launches and Shift-walk works with required 32-bit runtime libraries |
| **Fedora Workstation x86_64 — GNOME Classic / Wayland** | ✅ Confirmed working | Proton 11 + DXVK/Vulkan; installer, launcher, gameplay, and Shift-walk confirmed |
| **Fedora Workstation x86_64 — Cinnamon / Wayland** | ⚠️ Playable with input caveat | Proton 11 + DXVK/Vulkan launches and plays, but Shift + movement does not trigger walking |

### Desktop/session compatibility

Desktop environment and display session can matter independently of distribution. Fedora testing is particularly useful: **GNOME Classic on Wayland works correctly with Proton 11**, including Shift-walk, while **Cinnamon on Wayland on the same Fedora installation launches and plays but does not recognize Shift-walk correctly**. Therefore neither Wayland nor Proton 11 should be considered globally incompatible.

Linux Mint has been confirmed working with **Cinnamon / X11**, so Cinnamon itself is not known to be generally incompatible. Fedora Cinnamon/X11 remains untested.

The installer and diagnostics emit a **non-blocking Cinnamon + Wayland warning** when that specific combination is detected. It does not block installation and does not label Wayland globally incompatible.

## Diagnostics

Installer log:

```text
~/.local/state/OSFR-Linux/installer.log
```

Read-only compatibility report:

```bash
./Sanctuary-Linux-Installer --diagnose
```

Preview the default installation plan without changing files:

```bash
./Sanctuary-Linux-Installer --dry-run
```

`--diagnose` reports OS, kernel, desktop/session, CPU, architecture, RAM, GPU, Steam type/path, discovered Proton builds, the stable-first recommended Proton build, 32-bit FreeType/OpenGL status, 64-bit/32-bit Vulkan loader status, the recommended graphics backend, compatibility warnings, and package guidance when known runtime prerequisites are missing. This is the preferred first attachment/text dump for compatibility issues.

Useful findings from real-machine testing:

- Free Realms requires 32-bit runtime support even on x86_64 Linux.
- Missing 32-bit FreeType can produce `Wine cannot find the FreeType font library`.
- Missing 32-bit OpenGL/Mesa can prevent the 32-bit game stack from appearing.
- If Proton reaches `fsync: up and running.` but nothing opens, check 32-bit prerequisites before changing unrelated components.
- Desktop/session combinations can affect modifier-key input behavior.
- Fedora GNOME Classic / Wayland + Proton 11 is confirmed working, including Shift-walk.
- Fedora Cinnamon / Wayland + Proton 11 currently has a Shift-walk issue.
- Linux Mint Cinnamon / X11 is confirmed working with normal controls.

## Testing and release safety

The repository contains xUnit regression tests plus installer and launcher smoke tests. Pushes to `main` and pull requests into `main` run the full unit/smoke suite without publishing. Releases are built again from an immutable version tag, and publishing is allowed only when that tag exactly matches the project version.

Repository owners must set **Settings → Actions → General → Workflow permissions** to **Read and write permissions** so the tag workflow can publish release assets.

CI also checks NuGet dependencies for known vulnerabilities, validates generated desktop entries, builds the patched launcher and self-contained installer, exercises packaged `--diagnose` and `--dry-run`, verifies hardware/OS/runtime/graphics diagnostic fields are present, and verifies the published SHA-256 checksum. Versioned releases are immutable: publishing fails if the version tag already exists.

Regression coverage includes install/uninstall safety, ownership and rollback behavior, Steam-library parsing, Proton runtime architecture, Steam type detection, Cinnamon/Wayland warning logic, graphics-backend and display-mode behavior, stable-first Proton selection, OS/CPU/RAM/GPU text parsing, `ldconfig` parsing, and ELF32/ELF64 runtime probing.

Release cleanup is handled by a dedicated prerelease cleanup workflow. Stable releases are never removed by that automation.

## Current compatibility work

The active work list is tracked in `TODO.md`. The code-side compatibility and diagnostics pass is now largely complete. The remaining work is primarily **real-machine validation**.

That includes Arch, Ubuntu, openSUSE, SteamOS/Steam Deck, Fedora Cinnamon/X11, integrated graphics, more AMD/Intel/NVIDIA hardware, clean WineD3D/OpenGL validation, more desktop/session combinations, and broader outside-user testing.

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

Original installer code in this repository is licensed under GNU AGPL-3.0-or-later; see `LICENSE.md`. See `OSFR-Launcher/LICENSE` for the complete upstream launcher license text and `THIRD_PARTY_NOTICES.md` for bundled-component provenance.

The launcher includes Discord Rich Presence integration through the Discord Game SDK. This project is not created by or endorsed by Discord.

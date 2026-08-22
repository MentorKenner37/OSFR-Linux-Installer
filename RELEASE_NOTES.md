# Sanctuary Linux Installer — Alpha 2

This alpha focuses on making compatibility information much more useful on real Linux systems.

## Highlights

- Steam and Proton detection with native Steam, Flatpak Steam, custom Steam libraries, Proton Experimental, standard Proton releases, and GE-Proton support
- Stable-first Proton recommendation policy: newest compatible standard Proton is preferred, with GE-Proton and Experimental retained as selectable fallbacks
- Actual host reporting for Linux distribution, kernel, CPU model, architecture, installed RAM, GPU, desktop environment, and Wayland/X11 session
- Compatibility diagnostics for 32-bit FreeType/OpenGL plus 64-bit and 32-bit Vulkan loader availability
- Native-versus-Flatpak Steam identification in diagnostics and the graphical installer
- Graphics-backend recommendation logic based on detected 32-bit Vulkan state, with the recommended backend preselected while remaining user-overridable
- Non-blocking Cinnamon + Wayland warning for the currently known Shift/modifier input caveat
- Runtime, graphics, OS, hardware, Steam, Proton, and compatibility details surfaced directly in the graphical installer
- Selectable Proton graphics backend: Vulkan via DXVK or OpenGL via WineD3D
- Proton runtime architecture checks to prevent incompatible ARM64/x86_64 selections
- Dedicated Proton prefix management for Open Source Free Realms
- Transactional launcher replacement with rollback after failed updates
- Interrupted-install recovery after crashes or power loss
- Structured installation ownership metadata with launcher SHA-256 verification
- Symlink, archive traversal, install-path, and conservative deletion protections
- Sanctuary desktop/application-menu branding
- xUnit regression tests plus installer and launcher safety smoke tests
- Main-branch builds gated by xUnit and both smoke suites
- Packaged CI diagnostics validated for OS/hardware and runtime/graphics compatibility output
- Verified self-contained Linux installer with published SHA-256 checksum

## Install

Download both files from this release:

```text
Sanctuary-Linux-Installer
Sanctuary-Linux-Installer.sha256
```

Verify the installer:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
```

Then run:

```bash
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

For a read-only environment report:

```bash
./Sanctuary-Linux-Installer --diagnose
```

## Requirements

- x86_64 / AMD64 Linux
- Graphical Linux desktop
- Steam installed and working
- A compatible Proton version installed
- 32-bit Linux runtime support for Free Realms
- Working graphics drivers
- Vulkan support is recommended for DXVK; OpenGL/WineD3D is available as a compatibility fallback
- Internet connection and sufficient disk space

### Debian / 32-bit runtime note

Real-machine Debian 13 x86_64 testing confirmed that Sanctuary can launch Open Source Free Realms successfully once the required 32-bit runtime stack is present.

A fresh Debian setup initially showed:

```text
Wine cannot find the FreeType font library.
```

Enabling i386 multiarch and installing the following packages resolved the launch blocker:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These are system dependencies and are not bundled into the installer. The compatibility advisor now probes the relevant 32-bit runtime state and can provide distro-family guidance when known prerequisites are missing.

## Compatibility notes

- **Linux Mint x86_64 — Cinnamon / X11:** confirmed working with normal in-game controls.
- **Debian 13 x86_64:** confirmed working with Steam and Proton after the required 32-bit runtime libraries were installed; Shift-walk confirmed.
- **Fedora Workstation x86_64 — GNOME Classic / Wayland + Proton 11 + DXVK/Vulkan:** confirmed working, including Shift-walk.
- **Fedora Workstation x86_64 — Cinnamon / Wayland + Proton 11 + DXVK/Vulkan:** launches and plays, but Shift + movement does not trigger walking in the tested configuration.
- The Fedora comparison shows that Wayland and Proton 11 are not globally incompatible. The current input caveat is associated with the tested Cinnamon + Wayland combination.
- Systems without detected 32-bit Vulkan loader support are recommended WineD3D/OpenGL as the safer default; users may override the recommendation.

## Alpha status

Linux Mint, Debian 13, and Fedora Workstation have been validated far enough to launch and play Open Source Free Realms through Sanctuary in known configurations. Arch-based systems, Ubuntu, openSUSE, SteamOS/Steam Deck, integrated graphics, additional GPU/driver combinations, and broader desktop/session combinations are still being validated before beta status.

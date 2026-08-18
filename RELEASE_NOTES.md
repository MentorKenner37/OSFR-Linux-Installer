# Sanctuary Linux Launcher — Alpha 1

This is the first public alpha release under the cleaned Sanctuary Linux Launcher versioning scheme.

## Highlights

- Steam and Proton detection with native Steam, Flatpak Steam, custom Steam libraries, Proton Experimental, standard Proton releases, and GE-Proton support
- Selectable Proton graphics backend: Vulkan via DXVK (recommended) or OpenGL via WineD3D for systems without usable Vulkan support
- Proton runtime architecture checks to prevent incompatible ARM64/x86_64 selections
- Dedicated Proton prefix management for Open Source Free Realms
- Transactional launcher replacement with rollback after failed updates
- Interrupted-install recovery after crashes or power loss
- Structured installation ownership metadata with launcher SHA-256 verification
- Symlink, archive traversal, install-path, and conservative deletion protections
- Sanctuary desktop/application-menu branding
- xUnit regression tests plus installer and launcher safety smoke tests
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

Real-machine Debian x86_64 testing confirmed that Sanctuary can launch Open Source Free Realms successfully once the required 32-bit runtime stack is present.

A fresh Debian setup initially showed:

```text
Wine cannot find the FreeType font library.
```

and later stopped after Proton reported `fsync: up and running.`. Enabling i386 multiarch and installing the following packages resolved the launch blocker:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These are system dependencies and are not bundled into Sanctuary.

## Compatibility notes

- **Linux Mint x86_64:** confirmed working with Steam and Proton.
- **Debian x86_64:** confirmed launching Open Source Free Realms with Steam and Proton after the required 32-bit runtime libraries were installed.
- Free Realms is a 32-bit Windows title, so fresh 64-bit Linux installations may need additional 32-bit userspace libraries.
- Systems without reliable Vulkan support can select the WineD3D/OpenGL graphics backend.
- Proton-version differences may affect legacy mouse capture or in-game camera behavior on some hardware. If the game renders correctly but camera/input behavior is broken, test another installed Proton version before changing unrelated system settings.

## Alpha status

Linux Mint and Debian x86_64 have now both been validated far enough to launch Open Source Free Realms through Sanctuary. Ubuntu, Fedora, Arch-based systems, SteamOS/Steam Deck, additional desktop environments, filesystems, input devices, and GPU/driver combinations are still being validated before beta status.

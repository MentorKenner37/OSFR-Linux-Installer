# Sanctuary Linux Launcher — Alpha 1

This is the first public alpha release under the cleaned Sanctuary Linux Launcher versioning scheme.

## Highlights

- Steam and Proton detection with native Steam, Flatpak Steam, custom Steam libraries, Proton Experimental, standard Proton releases, and GE-Proton support
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
- Working graphics drivers with 32-bit Vulkan support
- Internet connection and sufficient disk space

## Alpha status

Linux Mint x86_64 is the primary tested environment. Debian and additional Linux distributions, Steam layouts, desktop environments, filesystems, and GPU/driver combinations are still being validated before beta status.

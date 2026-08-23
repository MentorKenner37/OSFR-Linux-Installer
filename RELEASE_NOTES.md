# Sanctuary Linux Installer 1.1.0-beta.6

This beta makes Gamescope fullscreen setup available directly inside the installer.

## What changed

- Detects Gamescope from PATH and the standard Linux binary locations `/usr/bin`, `/usr/games`, and `/usr/local/bin`.
- When Gamescope is already installed, shows a checked, disabled **Gamescope — Already installed** option.
- When Gamescope is missing, offers **Install Gamescope for real fullscreen**, enabled by default.
- Supports administrator-approved installation of the official `gamescope` package through DNF/DNF5, APT, Pacman, or Zypper.
- Uses fixed package-manager arguments, checks the package process exit code, and verifies the Gamescope executable after installation.
- Exposes the same Gamescope option during both new installation and repair/upgrade.
- Keeps normal windowed launching available and retains the safe fallback if Gamescope is unavailable.
- Includes Gamescope detection details in the installer summary and exported diagnostics.

## Existing protections

- HTTPS-only manifests, authentication, client downloads, and the verified `curl` compatibility fallback.
- Exact manifest size and XXHash64 verification before atomic client-file replacement.
- Transactional launcher replacement with rollback and interrupted-install recovery.
- Structured ownership, symlink, traversal, archive, install-path, and conservative deletion protections.
- xUnit regression tests, executable smoke tests, dependency vulnerability checks, self-contained packaging, and published SHA-256 checksums.

## Known beta issues

- Fedora Cinnamon/Wayland has a known Shift+movement caveat; Fedora GNOME Classic/Wayland is confirmed working.
- A machine-specific Debian camera/relative-mouse issue remains under investigation and was not reproduced on a second Debian 13 system.
- Hardware, Steam layout, Proton, desktop/session, and distribution coverage is still expanding.

## Install

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

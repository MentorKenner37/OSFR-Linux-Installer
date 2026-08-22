# Sanctuary Linux Installer 1.1.0-beta.1

This is the first public beta of Sanctuary Linux Installer. It keeps the verified v1.0.8 client-download recovery while strengthening its tests, diagnostics, and release process.

## Beta changes

- Resolves `curl` through `PATH` instead of assuming `/usr/bin` or `/bin`.
- Restricts both initial curl requests and redirects to HTTPS.
- Adds executable smoke coverage for curl argument safety, HTTPS rejection, PATH discovery, successful verified download, and hash-mismatch rejection.
- Reports curl fallback availability in graphical details, `--diagnose`, and `--dry-run`.
- Separates normal push/PR CI from immutable tag-triggered release publishing.
- Requires release tags to exactly match the project version.
- Generalizes superseded-prerelease cleanup without touching stable releases.

## Existing protections

- Transactional launcher replacement with rollback and interrupted-install recovery.
- Structured ownership, symlink, traversal, install-path, and conservative deletion protections.
- Dedicated Proton prefix with DXVK/Vulkan or WineD3D/OpenGL selection.
- HTTPS-only manifests and credentials, bounded manifests, protected credential storage, and verified atomic client downloads.
- xUnit regression tests, smoke tests, dependency vulnerability checks, self-contained packaging, and published SHA-256 checksums.

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

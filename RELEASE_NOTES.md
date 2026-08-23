# Sanctuary Linux Installer 1.1.0-beta.3

This beta adds selectable fullscreen and boxed-window game modes while retaining the complete maintenance experience and verified client-download protections from earlier betas.

## What changed

- Adds **Fullscreen (desktop resolution)** as the default for new installations.
- Adds an isolated **Boxed window (1280 × 720)** option through Proton's virtual desktop.
- Applies the saved display mode every time the launcher starts Free Realms without modifying `FreeRealms.exe`.
- Uses Gamescope for fullscreen when available and a desktop-sized Proton virtual desktop as the compatibility fallback.
- Allows existing installations to change display mode immediately from the maintenance page without reinstalling.
- Preserves the display-mode selection during repair and upgrade.
- Detects an existing Sanctuary installation immediately and replaces the normal wizard with a locked maintenance page.
- Adds **Launch Sanctuary**, **Repair / Upgrade**, **Uninstall**, **Open install folder**, **Open logs**, and **Export diagnostics** actions.
- Displays the installed version and current desktop-shortcut state.
- Adds a **Create a desktop shortcut** choice while always preserving application-menu integration.
- Adds a **Launch Sanctuary after installation** choice.
- Preserves downloaded game files, launcher settings, logs, and user data during a normal uninstall.
- Offers a separate explicit option to remove all Sanctuary user data.
- Recognizes damaged installation metadata for repair, while keeping recursive uninstall disabled until ownership and the launcher hash verify again.
- Exports a bounded diagnostic ZIP with credentials, authorization values, session IDs, cookies, passwords, and token query parameters redacted.

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

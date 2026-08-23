# Sanctuary Linux Installer 1.1.0-beta.5

This hotfix beta replaces the incorrect blue Proton virtual-desktop fallback with real Gamescope fullscreen and a safe normal-window fallback.

## What changed

- Removes Proton virtual-desktop launching from both display modes.
- Uses Gamescope around Proton for real scaled fullscreen.
- Launches Free Realms directly through Proton for normal windowed mode.
- When Gamescope is missing, falls back to the normal game window and displays a clear launcher notification instead of opening a blue desktop.
- Fixes a startup `NullReferenceException` caused by the display-mode event firing before Avalonia finished constructing the installer window.
- Defers display-mode event subscriptions until after `InitializeComponent()` completes.
- Adds **Fullscreen (desktop resolution)** as the default for new installations.
- Adds a **Windowed (game default)** option that launches directly through Proton.
- Applies the saved display mode every time the launcher starts Free Realms without modifying `FreeRealms.exe`.
- Uses Gamescope for fullscreen and safely falls back to the normal game window when Gamescope is unavailable.
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

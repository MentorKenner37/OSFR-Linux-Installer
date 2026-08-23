# Sanctuary Linux Installer 1.1.0-beta.9

This hotfix makes Free Realms reliably see the native fullscreen startup switch and retains the verified update system introduced in beta.8.

## Native display modes

- Removes Gamescope from every Sanctuary installation, launch, repair, diagnostic, and uninstall path.
- Always launches Free Realms directly through Proton.
- Fixes native fullscreen startup by placing the client's supported `--fullscreen` switch before its legacy `Server=...` and session arguments.
- Leaves the option off for a genuine movable game window with functional in-game size controls.
- Preserves the selected startup mode across repair and automatic upgrades.
- Migrates beta.5–beta.7 display settings and removes only obsolete Sanctuary Gamescope metadata.
- Never removes an independently installed Gamescope package or changes system repositories.
- Keeps the `ldconfig` working-directory diagnostics fix from beta.7.

## Verified updates

- Checks the official GitHub repository for updates when the Sanctuary launcher starts, at most twice per day.
- Supports stable-only or beta update channels.
- Shows update availability and summarized release notes without blocking offline play.
- Adds **Check now**, **Update now**, **Skip version**, and opt-in automatic updates to launcher settings.
- Downloads both the installer and its published checksum and refuses to execute a SHA-256 mismatch.
- Performs verified upgrades through the installer's existing transactional replacement and rollback system.
- Preserves Proton selection, graphics backend, display mode, desktop shortcut, prefix, downloaded game files, launcher settings, logs, and user data.
- Refuses automatic downgrades, wrong/unowned installation roots, and releases without both required assets.

## Install

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

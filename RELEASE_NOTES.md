# Sanctuary Linux Installer 1.1.0-beta.7

This beta fixes Gamescope installation on Debian 13 and adds ownership-safe Gamescope removal.

## What changed

- Installs Gamescope on Debian 13 from the official `trixie-backports` repository.
- Enables the official backports source with administrator approval only when APT has no Gamescope candidate.
- Uses `trixie-backports` explicitly instead of attempting an unavailable Debian stable package.
- Captures package-manager output and reports the actual installation error instead of only an exit code.
- Treats Gamescope as optional: a failed Gamescope setup no longer aborts Sanctuary installation.
- Records when Sanctuary installed Gamescope and offers to remove that system package during Sanctuary uninstall.
- Never offers automatic removal for a Gamescope installation that existed before Sanctuary installed.
- Removes the Sanctuary-created Debian backports source when removing an installer-owned Gamescope package.
- Clarifies that changing resolution inside Free Realms does not leave Gamescope fullscreen; true windowed mode must be selected in the installer.
- Fixes `ldconfig` diagnostics when the directory used to launch the installer has subsequently been removed.

## Install

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

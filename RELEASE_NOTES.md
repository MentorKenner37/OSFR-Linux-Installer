# OSFR Linux Installer v0.2.1-alpha

This alpha focuses on portability, safety, cleanup, and a more polished Linux installation experience.

## Highlights

- Refreshed professional dark installer interface with restrained gaming-inspired styling.
- Pure C# / Avalonia installer; no Python or Bash installer runtime is required.
- Self-contained Linux x64 distribution.
- Native and Flatpak Steam detection, including both common Flatpak data paths.
- Proton Experimental, standard Proton, custom Steam-library, and GE-Proton discovery with version-aware selection.
- Dedicated Proton prefix and explicit Steam/Proton path handoff to the launcher.
- Linux-only launcher path cleaned of obsolete Wine, Windows, macOS, DirectX, and Velopack updater code.
- Removed unused packages, platform binaries, build scripts, workflows, and stale documentation.
- Safer custom installation folders: unrelated non-empty directories are rejected.
- Uninstall refuses to recursively delete a directory unless it can verify that the directory belongs to OSFR.
- Hardened server/client filesystem boundaries against path traversal from manifests or tampered settings.
- Session IDs are no longer written to launcher logs.
- Remembered-password storage uses current-user-only permissions on Linux.
- Desktop launcher paths are safely quoted for custom install locations.
- Added installer and launcher path-safety smoke tests to CI.
- GitHub Actions build/test jobs use read-only repository permissions; write access is isolated to release publishing.
- Added an optional, enabled-by-default close-after-success setting in the installer.

## Requirements

- x86_64 / AMD64 Linux
- Graphical Linux desktop
- Steam installed and working
- An installed Proton build
- Up-to-date graphics drivers with the 32-bit Vulkan support needed by Steam Proton
- Internet connection and enough disk space for downloaded OSFR clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This is still a prerelease. Additional real-machine testing on other Linux distributions, Flatpak Steam installations, custom Steam libraries, and varied GPU/driver combinations is encouraged before a stable release.

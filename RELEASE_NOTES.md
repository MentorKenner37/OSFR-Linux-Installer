# OSFR Linux Installer v0.2.2-alpha

This alpha is a security, robustness, and diagnostics hardening release.

## Highlights

- Added a Proton version selector while keeping the recommended detected build selected by default.
- Added `--diagnose` and `--dry-run` command-line modes.
- Added a per-user installer log at `~/.local/share/OSFR-Linux/installer.log`.
- Replaced silent Steam/Proton detection failures with targeted error handling and diagnostics.
- Improved Steam library parsing and added coverage for custom library paths.
- Added version-aware Proton candidate testing and manual Proton override support.
- Rejects symbolic links as installation roots and critical launcher paths.
- Stages launcher extraction and verifies it before replacing an existing launcher.
- Rejects rooted, traversal, and symbolic-link entries in the embedded launcher archive.
- Uses no-follow recursive deletion so symlinks are removed as links rather than traversed.
- Verifies fixed uninstall targets remain inside the user's home directory.
- Removed broad process-name killing from uninstall.
- Improved errors for filesystems that cannot provide required Unix execute permissions.
- Expanded installer security smoke tests for traversal, symlink, Steam VDF, and Proton-selection cases.
- Keeps existing launcher path-safety tests, credential protections, session-token redaction, and least-privilege CI permissions.
- Added Discord Rich Presence attribution/disclaimer information to the project documentation.

## Requirements

- x86_64 / AMD64 Linux
- Graphical Linux desktop
- Steam installed and working
- An installed Proton build
- Up-to-date graphics drivers with the 32-bit Vulkan support needed by Steam Proton
- Internet connection and enough disk space for downloaded OSFR clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This is still a prerelease. Additional real-machine testing on other Linux distributions, Flatpak Steam installations, custom Steam libraries, SteamOS, and varied GPU/driver combinations is encouraged before a stable release.

# Sanctuary Linux Installer v0.2.13-alpha

This alpha hardens failure recovery and completes the visible Sanctuary launcher branding cleanup.

## Highlights

- Launcher replacement is now transactional. Existing launchers and ownership metadata are backed up before promotion, and a failed update rolls back to the previous working installation.
- Interrupted installs now leave a small transaction journal that distinguishes preparation, active replacement, and committed states so a later installer run can recover safely after a crash or power loss.
- New installs are not considered owned until launcher extraction, Proton configuration, desktop integration, and final verification have all succeeded.
- Ownership metadata now records the installer version and SHA-256 hash of the installed launcher. A modified launcher no longer satisfies the strongest ownership check.
- Ownership marker writes use randomized temporary files, write-through flushing, restrictive permissions, and atomic replacement.
- Valid legacy ownership metadata is automatically migrated to the stronger structured format when an existing installation is recognized.
- Install paths below symbolic-link ancestors are rejected, tightening destructive filesystem boundaries.
- Destructive filesystem helpers and transaction logic are separated from the main install flow to make the code easier to audit and maintain.
- The installed desktop shortcut and application-menu display name now show **Sanctuary** instead of **Open Source Free Realms**. Internal compatibility identifiers such as `OSFRLauncher` remain unchanged where required.

## Regression coverage

- Transaction rollback restores the previous launcher, ownership marker, and install metadata.
- Ownership verification detects launcher tampering through its recorded SHA-256 hash.
- Symbolic-link ancestors are rejected even when the final install directory does not exist yet.
- Generated desktop entries are required to use `Name=Sanctuary` and must not expose the legacy Open Source Free Realms shortcut name.
- Existing Proton architecture, path traversal, non-empty-directory, symlink, desktop validation, packaged-installer, vulnerability, and checksum tests remain in CI.

## Verify the download

Download both release files into the same directory and run:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
```

A successful verification reports `Sanctuary-Linux-Installer: OK`.

## Requirements

- x86_64 / AMD64 Linux
- Graphical Linux desktop
- Steam installed and working
- A compatible installed Proton build
- Working graphics drivers with 32-bit Vulkan support
- Internet connection and enough disk space for downloaded Open Source Free Realms clients

Linux Mint x86_64 with a working Steam + Proton setup remains the primary tested environment.

## Alpha status

This remains a prerelease. Fresh-install Linux Mint testing and broader validation across Debian, Ubuntu, Fedora, Arch, SteamOS, Flatpak Steam, custom Steam libraries, and varied GPU/driver combinations are still recommended before beta/stable status.

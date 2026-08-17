# Sanctuary Linux Installer v0.2.12-alpha

This alpha focuses on runtime validation, safer ownership tracking, and more reliable reinstall/uninstall behavior.

## Highlights

- Proton detection now records runtime architecture and compatibility instead of treating every discovered `proton` file as usable.
- ARM64/aarch64 Proton builds are rejected on x86_64 systems and cannot become the recommended or selected runtime.
- Each Proton candidate is paired with the Steam root that discovered it so the selected Proton and Steam compatibility path stay consistent.
- The launcher now treats the installer-selected Proton and Steam paths as authoritative instead of running a second independent Proton discovery routine.
- The last successful custom Sanctuary install location is persisted and restored on later installer launches after validation.
- Installation ownership now uses structured metadata bound to the canonical install root, install ID, and expected launcher path before recursive uninstall is authorized.
- Existing older installations remain supported through a constrained legacy ownership check.
- Existing staged extraction, archive traversal protection, symlink rejection, non-following recursive deletion, dedicated Proton prefix, diagnostics, desktop integration, and checksum verification remain in place.

## New regression coverage

- Explicit x86_64 vs aarch64 Proton compatibility test using ELF runtime architecture headers.
- Incompatible Proton builds cannot become recommended or make the installer ready.
- Copying a valid ownership marker to a different directory does not authorize uninstall there.
- Existing non-empty-directory, symlink, archive traversal, launcher path, desktop-entry, packaged-installer, and checksum smoke tests continue to run in CI.

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

This remains a prerelease. Fresh-install Linux Mint testing and broader validation across Ubuntu, Fedora, Arch, SteamOS, Flatpak Steam, custom Steam libraries, and varied GPU/driver combinations are still recommended before beta/stable status.

# Sanctuary Linux Installer — Alpha 3

Alpha 3 makes the installer much more useful for real compatibility testing by showing the actual system it is running on instead of only reporting generic compatibility states.

## Highlights

- The graphical compatibility panel now reports the detected Linux distribution instead of only showing `SUPPORTED`
- CPU model and architecture are shown directly in the installer
- Desktop environment and Wayland/X11 session are detected and displayed
- GPU hardware is detected and surfaced in the compatibility details
- Installed RAM, kernel, Steam type/location, and selected Proton build are included in detailed diagnostics
- `--diagnose` now provides a much more useful environment report for bug reports and tester feedback
- Steam and Proton detection supports native Steam, Flatpak Steam, custom Steam libraries, Proton Experimental, standard Proton releases, and GE-Proton
- Stable-first Proton recommendation policy prefers the newest compatible standard Proton while retaining GE-Proton and Experimental as selectable fallbacks
- Compatibility diagnostics cover 32-bit FreeType/OpenGL plus 64-bit and 32-bit Vulkan loader availability
- Graphics-backend recommendation logic uses detected Vulkan/runtime state while remaining user-overridable
- Non-blocking Cinnamon + Wayland warning remains in place for the known Shift/modifier input caveat
- Selectable graphics backend: Vulkan via DXVK or OpenGL via WineD3D
- Proton runtime architecture checks prevent incompatible ARM64/x86_64 selections
- Dedicated Proton prefix management for Open Source Free Realms
- Transactional launcher replacement with rollback and interrupted-install recovery
- Structured installation ownership metadata with launcher SHA-256 verification
- Symlink, archive traversal, install-path, and conservative deletion protections
- xUnit regression tests plus installer and launcher safety smoke tests
- Verified self-contained Linux installer with published SHA-256 checksum

## Install

Download both files from this release:

```text
Sanctuary-Linux-Installer
Sanctuary-Linux-Installer.sha256
```

Verify the installer:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
```

Then run:

```bash
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

For a read-only system and compatibility report:

```bash
./Sanctuary-Linux-Installer --diagnose
```

## Confirmed compatibility so far

- **Linux Mint x86_64 — Cinnamon / X11:** confirmed working with normal in-game controls
- **Debian 13 x86_64:** confirmed working with Steam and Proton after the required 32-bit runtime libraries are installed; Shift-walk confirmed
- **Fedora Workstation x86_64 — GNOME Classic / Wayland + Proton 11 + DXVK/Vulkan:** confirmed working, including Shift-walk
- **Fedora Workstation x86_64 — Cinnamon / Wayland + Proton 11 + DXVK/Vulkan:** launches and plays, but Shift + movement does not trigger walking in the tested configuration

The Fedora comparison demonstrates that neither Wayland nor Proton 11 is globally incompatible. The current modifier-key caveat is associated with the tested Cinnamon + Wayland combination.

## Debian / 32-bit runtime note

Free Realms is a 32-bit Windows application and requires the corresponding 32-bit Linux runtime stack even on x86_64 systems. A fresh Debian test initially reported:

```text
Wine cannot find the FreeType font library.
```

The known-working Debian package set is:

```bash
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install libfreetype6:i386 libgl1:i386 libgl1-mesa-dri:i386 libglx-mesa0:i386
```

These remain system dependencies and are not silently installed by Sanctuary Linux Installer.

## Alpha status

Linux Mint, Debian 13, and Fedora Workstation have been validated in-game in known configurations. Arch-based systems, Ubuntu, openSUSE, SteamOS/Steam Deck, integrated graphics, additional GPU/driver combinations, and broader desktop/session combinations are still being validated before beta status.

# Sanctuary Linux Installer

A self-contained Linux installer for Open Source Free Realms that configures the game to run through Steam Proton.

## Requirements

- x86_64 / AMD64 Linux
- A graphical desktop environment
- Steam installed and working
- At least one installed Proton version
- Working graphics drivers with the 32-bit Vulkan support required by Steam Proton
- An internet connection
- Enough free disk space for the launcher, Proton prefix, and game files

Release builds are self-contained. You do **not** need Python, installer scripts, or the .NET SDK.

> Sanctuary Linux Installer does not install, update, or modify Steam, Proton, or your graphics drivers.

## What Sanctuary Linux Installer does

- Checks Linux and x86_64 compatibility
- Detects native and Flatpak Steam installations
- Detects Proton Experimental, standard Proton releases, and compatibility tools such as GE-Proton
- Automatically recommends a Proton build while allowing you to choose another detected version
- Installs the Linux Open Source Free Realms launcher
- Creates a dedicated Proton prefix for Free Realms
- Configures the launcher to use the selected Steam and Proton installation
- Creates application-menu and desktop shortcuts
- Starts the launcher when installation finishes
- Writes installer diagnostics to `~/.local/state/OSFR-Linux/installer.log`
- Rotates the installer log at about 1 MiB and keeps up to three previous copies
- Safely removes the Sanctuary installation, its dedicated Proton prefix, downloaded clients, data, caches, and shortcuts when uninstalling

The installer rejects symbolic-link install roots, stages launcher extraction before replacing an existing launcher, rejects unsafe archive paths and archive symlinks, and will not recursively delete an installation directory unless it can verify that the directory belongs to this installation.

Internal OSFR data paths and launcher identifiers are retained for compatibility with existing installations.

## Download and install

1. Download `Sanctuary-Linux-Installer` and `Sanctuary-Linux-Installer.sha256` from the **Releases** page.
2. Verify the downloaded installer:

   ```bash
   sha256sum -c Sanctuary-Linux-Installer.sha256
   ```

3. Make the installer executable if necessary:

   ```bash
   chmod +x Sanctuary-Linux-Installer
   ```

4. Run it:

   ```bash
   ./Sanctuary-Linux-Installer
   ```

5. Confirm that Linux, x86_64, Steam, and Proton are detected under **System Compatibility**.
6. Choose an installation location.
7. Choose the Proton version you want to use. The recommended detected version is selected automatically.
8. Review the installation summary and select **Install**.

The launcher handles Open Source Free Realms login and downloads the client files required by the selected server.

## Diagnostics

Run a read-only system check from a terminal:

```bash
./Sanctuary-Linux-Installer --diagnose
```

Preview the default installation plan without changing files:

```bash
./Sanctuary-Linux-Installer --dry-run
```

Installation and detection errors are written to:

```text
~/.local/state/OSFR-Linux/installer.log
```

The diagnostics directory is intentionally separate from the default installation directory (`~/.local/share/OSFR-Linux`) so launching the installer does not make the default destination appear occupied.

The log is capped by rotation at about 1 MiB per file with up to three previous copies retained. Passwords and session tokens are not intentionally written to installer diagnostics.

## Compatibility

### Tested

- Linux Mint x86_64 with Steam and Proton

### Planned testing

- Ubuntu
- Fedora
- Arch-based distributions
- SteamOS / Steam Deck
- Additional desktop environments and GPU/driver configurations

The installer already supports common native Steam locations, Flatpak Steam locations, custom Steam libraries, Proton Experimental, standard Proton releases, and GE-Proton. Other distributions still need real-machine testing before they are listed as tested.

## Release verification

The GitHub Actions release pipeline verifies the same installer artifact that is ultimately published. It:

- runs installer and launcher safety tests
- checks direct and transitive NuGet dependencies for known vulnerabilities
- validates the generated Linux `.desktop` entry with `desktop-file-validate`
- builds the patched Open Source Free Realms launcher and embeds it into the installer
- validates the official installer icon
- builds the self-contained `linux-x64` installer
- runs the packaged installer with `--diagnose` and `--dry-run`
- generates and verifies `Sanctuary-Linux-Installer.sha256`
- downloads the tested artifact in the release job and verifies its checksum again before publication

Each release contains both:

```text
Sanctuary-Linux-Installer
Sanctuary-Linux-Installer.sha256
```

## Building from source

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

Official release builds use the SDK pinned by that file on GitHub's Ubuntu runner, target `linux-x64`, and publish both the launcher and installer as self-contained .NET applications. The installer itself is published as a single-file executable with native libraries included for self-extraction.

End users should download the packaged installer from **Releases** rather than build the project themselves.

## Project structure

- `src/Installer/` — C# Avalonia Sanctuary Linux Installer
- `src/Installer.SmokeTests/` — installer safety tests
- `src/Launcher.SmokeTests/` — launcher path-safety tests
- `OSFR-Launcher/` — modified Open Source Free Realms launcher source
- `Directory.Build.props` — shared version and product information
- `.github/workflows/build-linux-installer.yml` — Linux build, verification, checksum, and release workflow

## Status

This project is currently in **alpha**. Linux Mint is the primary tested platform. Additional distributions will be added to the tested list as they are verified on real hardware.

## Credits

Based on the Open Source Free Realms Launcher project. See `OSFR-Launcher/LICENSE` for the upstream launcher license.

The launcher includes Discord Rich Presence integration using the Discord Game SDK. This project is not endorsed by or created by Discord.

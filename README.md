# Open Source Free Realms - Linux Installer

A self-contained Linux installer for Open Source Free Realms that runs the game through Steam Proton.

## Requirements

- x86_64 / AMD64 Linux
- A graphical desktop environment
- Steam installed and working
- At least one installed Proton version
- Working graphics drivers with the 32-bit Vulkan support required by Steam Proton
- An internet connection
- Enough free disk space for the launcher, Proton prefix, and game files

Release builds are self-contained. You do **not** need Python, installer scripts, or the .NET SDK.

> The installer does not install, update, or modify Steam, Proton, or your graphics drivers.

## What the installer does

- Checks Linux and x86_64 compatibility
- Detects native and Flatpak Steam installations
- Detects Proton Experimental, standard Proton releases, and compatibility tools such as GE-Proton
- Automatically recommends a Proton build while allowing you to choose another detected version
- Installs the Linux OSFR Launcher
- Creates a dedicated Proton prefix for Free Realms
- Configures the launcher to use the selected Steam and Proton installation
- Creates application-menu and desktop shortcuts
- Starts the OSFR Launcher when installation finishes
- Writes installer diagnostics to `~/.local/share/OSFR-Linux/installer.log`
- Safely removes OSFR, its dedicated Proton prefix, downloaded clients, data, caches, and shortcuts when uninstalling

The installer rejects symbolic-link install roots, stages launcher extraction before replacing an existing launcher, rejects unsafe archive paths and archive symlinks, and will not recursively delete an installation directory unless it can verify that the directory belongs to this OSFR installation.

## Download and install

1. Download `OSFR-Linux-Installer` from the **Releases** page.
2. Make it executable if necessary:

   ```bash
   chmod +x OSFR-Linux-Installer
   ```

3. Run it:

   ```bash
   ./OSFR-Linux-Installer
   ```

4. Confirm that Linux, x86_64, Steam, and Proton are detected.
5. Choose the Proton version you want to use. The recommended detected version is selected automatically.
6. Choose an installation folder and select **Install**.
7. The OSFR Launcher will open when installation completes.

The launcher handles OSFR login and downloads the client files required by the selected server.

## Diagnostics

Run a read-only system check from a terminal:

```bash
./OSFR-Linux-Installer --diagnose
```

Preview the default installation plan without changing files:

```bash
./OSFR-Linux-Installer --dry-run
```

Installation and detection errors are also written to:

```text
~/.local/share/OSFR-Linux/installer.log
```

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

## Building from source

Development builds require the .NET SDK specified by `OSFR-Launcher/src/global.json`.

The GitHub Actions pipeline runs installer and launcher safety tests, builds the patched OSFR Launcher for `linux-x64`, embeds it into the installer, builds the self-contained installer, and publishes the release artifact.

End users should download the packaged installer from **Releases** rather than build the project themselves.

## Project structure

- `src/Installer/` — C# Avalonia installer
- `src/Installer.SmokeTests/` — installer safety tests
- `src/Launcher.SmokeTests/` — launcher path-safety tests
- `OSFR-Launcher/` — modified OSFR Launcher source
- `Directory.Build.props` — shared version information
- `.github/workflows/build-linux-installer.yml` — Linux build and release workflow

## Status

This project is currently in **alpha**. Linux Mint is the primary tested platform. Additional distributions will be added to the tested list as they are verified on real hardware.

## Credits

Based on the Open Source Free Realms Launcher project. See `OSFR-Launcher/LICENSE` for the upstream launcher license.

The launcher includes Discord Rich Presence integration using the Discord Game SDK. This project is not endorsed by or created by Discord.

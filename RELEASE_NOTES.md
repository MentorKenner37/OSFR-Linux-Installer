# OSFR Linux Installer v0.2.3-alpha

This alpha adds release-integrity verification and a small diagnostics/logging hardening pass on top of v0.2.2-alpha.

## Highlights

- Publishes `OSFR-Linux-Installer.sha256` beside the installer on every release.
- Verifies the SHA-256 checksum immediately after building and again after the release job downloads the tested artifact.
- Runs the packaged single-file installer with `--diagnose` and `--dry-run` before publication.
- Checks direct and transitive NuGet dependencies for known vulnerabilities during CI.
- Validates the generated Linux `.desktop` entry with `desktop-file-validate`.
- Rotates `installer.log` at about 1 MiB and retains up to three previous log files.
- Keeps logging failures non-fatal while reporting them to standard error when possible.
- Documents checksum verification and the official release-build environment in the README.
- Retains the v0.2.2 security hardening: Proton selection, path/symlink protections, staged extraction, safe no-follow deletion, diagnostics, launcher path protections, credential protections, and least-privilege release permissions.

## Verify the download

Download both release files into the same directory and run:

```bash
sha256sum -c OSFR-Linux-Installer.sha256
```

A successful verification reports `OSFR-Linux-Installer: OK`.

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

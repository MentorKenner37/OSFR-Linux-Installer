# Sanctuary Linux Installer 1.0.6

Version 1.0.6 adds exact client-download diagnostics and requests an identity-encoded response to isolate a Linux HTTP-stream discrepancy affecting `loading.html`.

## Security and data-safety improvements

- Uses direct HTTP response streaming for client files, eliminating the stream behavior that caused an erroneous `Failed to download ... loading.html` result.
- Records the final URL, HTTP version, expected and received byte counts, content length, content encoding, and transfer encoding for failed downloads.
- Explicitly requests an uncompressed identity response to match the known-working command-line download behavior.
- Uninstall now preserves shared OSFR launcher settings, downloaded clients, and credential storage.
- Client files download to unique temporary files, pass their manifest size and hash checks, and only then replace installed files atomically.
- Server manifests, login, and registration now require HTTPS.
- Server and client manifests have a 1 MiB download limit.
- Versioned GitHub releases are immutable and cannot silently replace assets attached to an existing tag.
- GitHub Actions dependencies are pinned to exact reviewed commits.
- Bundled Discord Game SDK provenance and SHA-256 are documented.

## Existing installer protections

- Transactional launcher replacement with rollback and interrupted-install recovery.
- Structured installation ownership metadata with launcher SHA-256 verification.
- Symlink, archive traversal, install-path, and conservative deletion protections.
- A dedicated Proton prefix and selectable DXVK/Vulkan or WineD3D/OpenGL backend.
- xUnit regression tests plus installer and launcher safety smoke tests.
- A self-contained Linux x86_64 installer with a published SHA-256 checksum.

## Install

Download both release files:

```text
Sanctuary-Linux-Installer
Sanctuary-Linux-Installer.sha256
```

Verify and launch:

```bash
sha256sum -c Sanctuary-Linux-Installer.sha256
chmod +x Sanctuary-Linux-Installer
./Sanctuary-Linux-Installer
```

For a read-only compatibility report:

```bash
./Sanctuary-Linux-Installer --diagnose
```

## Confirmed compatibility

- Linux Mint x86_64 — Cinnamon/X11.
- Debian 13 x86_64 with the required 32-bit runtime libraries.
- Fedora Workstation x86_64 — GNOME Classic/Wayland with Proton 11 and DXVK.
- Fedora Workstation x86_64 — Cinnamon/Wayland launches and plays, with a known Shift+movement caveat.

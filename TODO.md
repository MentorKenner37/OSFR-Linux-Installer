# TODO

## Compatibility, diagnostics, and release safety

- [x] Report distribution, kernel, CPU, RAM, GPU, desktop environment, session type, Steam path, and Proton builds in diagnostics.
- [x] Show detected OS/hardware instead of generic supported/compatible labels in the installer.
- [x] Run xUnit plus installer and launcher smoke tests on pushes to `main` and pull requests.
- [x] Gate packaged installer builds on the xUnit regression suite.
- [x] Remove obsolete version-specific release cleanup from the build workflow; keep cleanup in the dedicated cleanup workflow.
- [x] Detect required 32-bit FreeType/OpenGL runtime support and provide distro-family package guidance when it is missing.
- [x] Detect 64-bit and 32-bit Vulkan loader availability and produce a DXVK/Vulkan versus WineD3D/OpenGL recommendation.
- [x] Identify native versus Flatpak Steam explicitly in diagnostics and the graphical installer.
- [x] Detect Cinnamon + Wayland and emit a non-blocking warning for the known Shift/modifier input caveat without treating Wayland itself as incompatible.
- [x] Surface runtime prerequisite checks, graphics recommendation, Steam type, and compatibility warnings directly in the graphical installer UI.
- [x] Prefer the newest stable compatible Proton release by default; keep Experimental and GE-Proton available as troubleshooting choices/fallbacks.
- [x] Add regression tests for Steam-layout detection, Cinnamon/Wayland warning logic, graphics-backend recommendation logic, stable-first Proton selection, host-info parsing, ldconfig parsing, and ELF32/ELF64 runtime probing.
- [x] Preserve shared launcher data during uninstall.
- [x] Stage and verify downloaded client files before atomic replacement.
- [x] Require HTTPS for server manifests and credential submission.
- [x] Bound remote manifest downloads to 1 MiB.
- [x] Make versioned releases immutable and pin GitHub Actions by commit SHA.
- [x] Document installer licensing and bundled native-component provenance.

## Real-machine validation

- [ ] Arch-based distributions
- [ ] Ubuntu
- [ ] openSUSE
- [ ] SteamOS / Steam Deck
- [ ] Fedora Cinnamon / X11
- [ ] Integrated graphics
- [ ] Additional AMD, Intel, and NVIDIA GPUs
- [ ] Clean-install WineD3D/OpenGL validation
- [ ] More desktop environments and Wayland/X11 combinations
- [ ] More native/Flatpak/custom-library Steam layouts
- [ ] Broader outside-user Alpha testing

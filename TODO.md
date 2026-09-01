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
- [x] Separate ordinary CI from tag-only release publishing.
- [x] Detect curl through PATH and add regression coverage for the verified fallback.
- [x] Recognize CachyOS, EndeavourOS, Manjaro, and Garuda as Arch-family systems for 32-bit package guidance.
- [x] Mark 32-bit FreeType and OpenGL as required runtime components in compatibility diagnostics; keep 32-bit Vulkan optional with WineD3D/OpenGL fallback.
- [x] Provide GPU-aware Arch-family Vulkan package guidance for NVIDIA, AMD/Radeon, and Intel graphics.
- [ ] Block fresh installation/repair when 32-bit FreeType or 32-bit OpenGL is definitely missing; do not block when the probe is unknown, and do not require Vulkan.

## Real-machine validation

- [x] CachyOS — KDE Plasma (outside tester; installer and game confirmed working after required 32-bit runtime libraries were installed)
- [x] CachyOS — Hyprland (outside tester; installer and game confirmed working after required 32-bit runtime libraries were installed)
- [ ] Vanilla Arch Linux
- [ ] EndeavourOS
- [ ] Manjaro
- [ ] Other Arch-based distributions
- [ ] Ubuntu
- [ ] openSUSE
- [ ] SteamOS / Steam Deck
- [ ] Fedora Cinnamon / X11
- [ ] Integrated graphics
- [ ] Additional AMD, Intel, and NVIDIA GPUs
- [ ] Clean-install WineD3D/OpenGL validation
- [ ] More desktop environments and Wayland/X11 combinations
- [ ] More native/Flatpak/custom-library Steam layouts
- [ ] Broader outside-user Beta testing

## Compatibility notes from outside testing

- CachyOS is confirmed working under both KDE Plasma and Hyprland.
- The initial CachyOS game launch failed until the tester installed the required 32-bit runtime libraries; after that, gameplay was reported as working flawlessly.
- This validates CachyOS only. It does **not** mark vanilla Arch Linux, EndeavourOS, Manjaro, or other Arch derivatives as tested.
- The tester had to create a new character; because the client then ran normally, this is currently treated as a likely server/account/character-state issue rather than a CachyOS or desktop-environment compatibility failure.

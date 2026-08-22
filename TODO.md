# TODO

## Alpha compatibility and diagnostics

- [x] Report distribution, kernel, CPU, RAM, GPU, desktop environment, session type, Steam path, and Proton builds in diagnostics.
- [x] Show detected OS/hardware instead of generic supported/compatible labels in the installer.
- [x] Run xUnit plus installer and launcher smoke tests on pushes to `main` and pull requests.
- [x] Gate packaged installer builds on the xUnit regression suite.
- [x] Remove obsolete version-specific release cleanup from the build workflow; keep cleanup in the dedicated cleanup workflow.
- [ ] Detect required 32-bit FreeType/OpenGL runtime support and provide distro-specific package guidance when it is missing.
- [ ] Detect Vulkan and 32-bit Vulkan availability and use that information when recommending DXVK/Vulkan versus WineD3D/OpenGL.
- [ ] Identify native versus Flatpak Steam explicitly in diagnostics.
- [ ] Add a non-blocking Cinnamon + Wayland warning for the known Shift/modifier input caveat without treating Wayland itself as incompatible.
- [ ] Prefer the newest stable compatible Proton release by default; keep Experimental and GE-Proton available as troubleshooting choices.
- [ ] Add regression tests for distro/session detection, hardware parsing, graphics capability recommendations, runtime prerequisite detection, and compatibility-warning logic.

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

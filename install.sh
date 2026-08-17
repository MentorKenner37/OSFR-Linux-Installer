#!/usr/bin/env bash
set -euo pipefail

# Resolve everything relative to this repository, not the user's home directory.
REPO_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
HOME_DIR="$HOME"
ROOT="${OSFR_INSTALL_DIR:-$HOME_DIR/.local/share/OSFR-Linux}"
BIN="$ROOT/Launcher"
PREFIX="$ROOT/ProtonPrefix"
CLIENT="$ROOT/Client"
LAUNCHER="$BIN/OSFRLauncher"
STATUS_FILE="${OS
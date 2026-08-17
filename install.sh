#!/usr/bin/env bash
set -euo pipefail

HOME_DIR="$HOME"

# User-selectable installation directory.
# Default: ~/.local/share/OSFR
ROOT="${OSFR_INSTALL_DIR:-$HOME_DIR/.local/share/OSFR}"

BIN="$ROOT/Launcher"
PREFIX="$ROOT/ProtonPrefix"
CLIENT="$ROOT/Client"
LAUNCHER="$BIN/OSFRLauncher"
STATUS_FILE="${OSFR_STATUS_FILE:-$ROOT/install-status}"

progress() {
    printf "%s|%s\n" "$1" "$2" > "$STATUS_FILE"
}

mkdir -p "$ROOT" "$BIN" "$PREFIX" "$CLIENT"

echo "=========================================="
echo "       OSFR LINUX INSTALLER"
echo "=========================================="
echo

# ------------------------------------------------------------
# Requirements
# ------------------------------------------------------------


progress 1 "Checking Linux..."
echo "[1/8] Checking Linux..."

if ! command -v apt >/dev/null 2>&1; then
    echo "This installer currently requires an Ubuntu/Debian based system."
    exit 1
fi

echo "      ✓ Linux"


progress 2 "Checking Steam..."
echo "[2/8] Checking Steam..."

if command -v steam >/dev/null 2>&1; then
    echo "      ✓ Steam"
else
    echo "Steam is required."
    echo "Install Steam first, then run this installer again."
    exit 1
fi


progress 3 "Finding Proton..."
echo "[3/8] Finding Proton..."

PROTON=""

for p in \
 "$HOME/.steam/debian-installation/steamapps/common/Proton - Experimental/proton" \
 "$HOME/.steam/debian-installation/steamapps/common/Proton Hotfix/proton" \
 "$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/proton" \
 "$HOME/.local/share/Steam/steamapps/common/Proton Hotfix/proton"
do
    if [ -f "$p" ]; then
        PROTON="$p"
        break
    fi
done

if [ -z "$PROTON" ]; then
    echo "No Steam Proton installation was found."
    echo "Open Steam and install Proton Experimental first."
    exit 1
fi

echo "      ✓ Proton"
echo "        $PROTON"


progress 4 "Checking 32-bit architecture..."
echo "[4/8] Checking 32-bit architecture..."

if command -v dpkg >/dev/null 2>&1; then
    if dpkg --print-foreign-architectures 2>/dev/null | grep -qx i386; then
        echo "      ✓ i386 enabled"
    else
        echo "      ✗ i386 is not enabled"
        echo "        Enable 32-bit support in Steam/your OS and run again."
        exit 1
    fi
else
    echo "      ✓ Architecture check skipped"
fi

echo "[5/8] Checking runtime dependencies..."

MISSING=""

for cmd in curl wget unzip python3; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        MISSING="$MISSING $cmd"
    fi
done

if ! command -v vulkaninfo >/dev/null 2>&1; then
    MISSING="$MISSING vulkan-tools"
fi

if [ -n "$MISSING" ]; then
    echo
    echo "ERROR: Required dependencies are missing:"
    echo "$MISSING"
    echo
    echo "Please install the missing packages with your normal"
    echo "Linux package manager, then run the installer again."
    exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo
    echo "ERROR: .NET SDK is required to build the patched launcher."
    echo "Please install the .NET 10 SDK and run the installer again."
    exit 1
fi

echo "      ✓ Runtime dependencies"

# ------------------------------------------------------------
# Native OSFR Launcher
# ------------------------------------------------------------


progress 6 "Building patched OSFR Launcher..."
echo "[6/8] Building patched OSFR Launcher..."

SOURCE="$HOME/OSFR-Launcher"
PROJECT="$SOURCE/src/Launcher/Launcher.csproj"

if [ ! -f "$PROJECT" ]; then
    echo
    echo "ERROR: OSFR Launcher source was not found:"
    echo "       $PROJECT"
    echo
    echo "The installer needs the patched OSFR-Launcher source."
    exit 1
fi

echo "      → Building Linux launcher from local source..."

dotnet build "$PROJECT" -c Release --nologo

BUILD_DIR="$SOURCE/src/Launcher/bin/Release/net10.0"

if [ ! -f "$BUILD_DIR/Launcher.dll" ]; then
    echo
    echo "ERROR: Patched launcher build failed."
    exit 1
fi

rm -rf "$BIN/publish"
mkdir -p "$BIN/publish"

echo "      → Publishing linux-x64 launcher..."

dotnet publish "$PROJECT" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$BIN/publish"

FOUND="$BIN/publish/Launcher"

if [ ! -f "$FOUND" ]; then
    echo
    echo "ERROR: Published Linux launcher was not found."
    find "$BIN/publish" -maxdepth 3 -type f -print
    exit 1
fi

# ------------------------------------------------------------
# Native Avalonia / Skia runtime
# ------------------------------------------------------------
# Avalonia loads libSkiaSharp by its native library name.
# Keep the Linux x64 library beside the launcher executable.
# This prevents DllNotFoundException on clean installations.

SKIA="$BIN/publish/libSkiaSharp.so"

if [ ! -f "$SKIA" ]; then
    SKIA_SOURCE="$BIN/publish/runtimes/linux-x64/native/libSkiaSharp.so"

    if [ -f "$SKIA_SOURCE" ]; then
        cp "$SKIA_SOURCE" "$SKIA"
    else
        echo
        echo "ERROR: Linux x64 libSkiaSharp.so was not produced."
        echo
        find "$BIN/publish" \
            -type f \
            -iname 'libSkiaSharp.so' \
            -print
        exit 1
    fi
fi

# Install the complete published application.
rm -rf "$BIN/installed"
mkdir -p "$BIN/installed"

cp -a "$BIN/publish/." "$BIN/installed/"

# Copy all published runtime files beside the final launcher.
cp -a "$BIN/installed/." "$BIN/"

# The published executable is named Launcher.
# Rename it to the stable OSFRLauncher path.
if [ ! -f "$BIN/Launcher" ]; then
    echo
    echo "ERROR: Published Launcher executable was not found."
    echo
    find "$BIN/publish" -maxdepth 2 -type f -print
    exit 1
fi

LAUNCHER="$BIN/OSFRLauncher"
mv "$BIN/Launcher" "$LAUNCHER"
chmod +x "$LAUNCHER"

# Verify the native Linux x64 Skia library is beside the executable.
if [ ! -f "$BIN/libSkiaSharp.so" ]; then
    echo
    echo "ERROR: libSkiaSharp.so is missing from the launcher directory."
    echo
    find "$BIN" -type f -iname 'libSkiaSharp.so' -print
    exit 1
fi

echo "      ✓ Patched Linux launcher installed"
echo "      ✓ Linux x64 Skia runtime installed"
echo "      ✓ Native launcher verification passed"

echo "      ✓ Patched Linux launcher installed"

# ------------------------------------------------------------
# Proton environment
# ------------------------------------------------------------


progress 8 "Configuring Proton..."
echo "[8/8] Configuring Proton..."

cat > "$BIN/osfr-launch.sh" <<EOF2
#!/usr/bin/env bash

export STEAM_COMPAT_DATA_PATH="$PREFIX"
export STEAM_COMPAT_CLIENT_INSTALL_PATH="$HOME/.steam/debian-installation"

exec "$LAUNCHER" "\$@"
EOF2

chmod +x "$BIN/osfr-launch.sh"

# ------------------------------------------------------------
# Desktop integration
# ------------------------------------------------------------

ICON="$BIN/OSFRLauncher.png"

if [ -f "$HOME/.local/opt/OSFRLauncher/OSFRLauncher.png" ]; then
    cp "$HOME/.local/opt/OSFRLauncher/OSFRLauncher.png" "$ICON"
fi

mkdir -p "$HOME/.local/share/applications"

cat > "$HOME/.local/share/applications/OSFR-Linux.desktop" <<EOF2
[Desktop Entry]
Type=Application
Name=Open Source Free Realms
Comment=Open Source Free Realms — Linux
Exec=$BIN/osfr-launch.sh
Icon=$ICON
Terminal=false
Categories=Game;
StartupNotify=true
EOF2

chmod +x "$HOME/.local/share/applications/OSFR-Linux.desktop"

mkdir -p "$HOME/Desktop"

cp \
 "$HOME/.local/share/applications/OSFR-Linux.desktop" \
 "$HOME/Desktop/OSFR-Linux.desktop"

chmod +x "$HOME/Desktop/OSFR-Linux.desktop"

# ------------------------------------------------------------
# Manifest
# ------------------------------------------------------------

cat > "$ROOT/install-info.txt" <<EOF2
OSFR Linux Installation

Launcher:
$LAUNCHER

Proton:
$PROTON

Proton Prefix:
$PREFIX

Client:
$CLIENT

Launcher Version:
1.1.5
EOF2


echo
echo "=========================================="
echo "          INSTALLATION COMPLETE"
echo "=========================================="
echo
echo "OSFR Launcher:"
echo "  $LAUNCHER"
echo
echo "Proton:"
echo "  $PROTON"
echo
echo "Prefix:"
echo "  $PREFIX"
echo
echo "Desktop shortcut:"
echo "  $HOME/Desktop/OSFR-Linux.desktop"
echo
echo "Launching OSFR Launcher..."
echo

# Launch independently so the installer can finish immediately.
nohup "$LAUNCHER" >/dev/null 2>&1 &

# Give the launcher a moment to start, then let the installer exit.
sleep 2

# Report successful completion to the GUI.
if [ -n "${OSFR_STATUS_FILE:-}" ]; then
    printf "%s|%s\\n" "8" "Installation complete" > "$OSFR_STATUS_FILE"
fi

exit 0


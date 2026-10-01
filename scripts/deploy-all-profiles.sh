#!/usr/bin/env bash
# Copies the built Jukebox.dll into every Gale REPO profile. Safe while the game runs: BepInEx
# loads the DLL fully at startup and never reads the file again, and the copy lands as an
# atomic rename.
set -euo pipefail

PROFILES="$HOME/.local/share/com.kesomannen.gale/repo/profiles"
SRC="$PROFILES/Developer/BepInEx/plugins/Jukebox"

[ -f "$SRC/Jukebox.dll" ] || { echo "No build at $SRC, run dotnet build first." >&2; exit 1; }

for profile in "$PROFILES"/*/; do
    dest="$profile/BepInEx/plugins/Jukebox"
    if [ "$(readlink -f "$dest/Jukebox.dll")" = "$(readlink -f "$SRC/Jukebox.dll")" ]; then
        echo "source   -> $(basename "$profile")"
        continue
    fi
    mkdir -p "$dest"
    cp -p "$SRC/Jukebox.dll" "$dest/.Jukebox.dll.staging"
    mv -f "$dest/.Jukebox.dll.staging" "$dest/Jukebox.dll"
    echo "deployed -> $(basename "$profile")"
done
if pgrep -x REPO.exe >/dev/null 2>&1; then
    echo "REPO is running: the new build is in place and takes effect on the next launch."
fi

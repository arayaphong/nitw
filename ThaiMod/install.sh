#!/bin/sh
# Install or remove the NITW Thai mod in a Night in the Woods (Linux) folder.
# Only adds new files; never modifies or overwrites the game's own files.
#   ./install.sh install   <game dir>
#   ./install.sh uninstall <game dir>
# Then launch with: <game dir>/run_nitwthai.sh
# (Steam: set the launch option to  ./run_nitwthai.sh %command% )
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
ACTION=${1:?install|uninstall}; GAME=${2:?game dir}
[ -d "$GAME/Night in the Woods_Data/Managed" ] || { echo "not a Night in the Woods folder: $GAME" >&2; exit 1; }
LIST="$GAME/NITWThai/installed.txt"
case "$ACTION" in
install)
    for f in NITWThai/NITWThai.Loader.dll doorstop_libs run_nitwthai.sh; do
        if [ -e "$GAME/$f" ] && [ ! -f "$LIST" ]; then
            echo "refusing: $GAME/$f exists and was not installed by this script" >&2; exit 1
        fi
    done
    mkdir -p "$GAME/NITWThai"
    cp "$HERE/out/NITWThai.Loader.dll" "$HERE/out/NITWThai.Runtime.dll" "$HERE/lib/Mono.Cecil.dll" "$GAME/NITWThai/"
    rm -rf "$GAME/doorstop_libs"; cp -a "$HERE/lib/doorstop_libs" "$GAME/"
    cp "$HERE/run_nitwthai.sh" "$GAME/"; chmod +x "$GAME/run_nitwthai.sh"
    printf '%s\n' NITWThai doorstop_libs run_nitwthai.sh > "$LIST"
    echo "installed into $GAME"
    ;;
uninstall)
    [ -f "$LIST" ] || { echo "no NITWThai install found in $GAME" >&2; exit 1; }
    while read -r item; do
        case "$item" in NITWThai|doorstop_libs|run_nitwthai.sh) rm -rf "$GAME/$item" ;; esac
    done < "$LIST"
    echo "removed from $GAME"
    ;;
*) echo "unknown action $ACTION" >&2; exit 1 ;;
esac

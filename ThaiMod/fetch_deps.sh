#!/bin/sh
# Fetch the third-party pieces the Thai mod needs but this repository does
# not ship: UnityDoorstop 3 libraries, Mono.Cecil and BepInEx's launcher
# script, all from the official BepInEx 5.4.22 Unix release (LGPL-2.1 /
# MIT). The launcher is adapted to start NITWThai/NITWThai.Loader.dll.
#   ./fetch_deps.sh
# Produces lib/doorstop_libs/, lib/Mono.Cecil.dll, run_nitwthai.sh and fonts/.
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
VERSION=5.4.22
ZIP=BepInEx_unix_$VERSION.0.zip
URL=https://github.com/BepInEx/BepInEx/releases/download/v$VERSION/$ZIP
SHA256=50676d784ea63331c6ba3b22b7a27ef33e81d2b32c3ccc38c0e7297ecff7d945
DL=$HERE/downloads/bepinex-$VERSION
mkdir -p "$DL/zip"
if [ ! -f "$DL/zip/$ZIP" ]; then
    echo "downloading $URL"
    curl -fsSL -o "$DL/zip/$ZIP.part" "$URL"
    mv "$DL/zip/$ZIP.part" "$DL/zip/$ZIP"
fi
echo "$SHA256  $DL/zip/$ZIP" | sha256sum -c -
rm -rf "$DL/extract"; mkdir -p "$DL/extract"
unzip -q "$DL/zip/$ZIP" -d "$DL/extract"
mkdir -p "$HERE/lib"
rm -rf "$HERE/lib/doorstop_libs"
cp -a "$DL/extract/doorstop_libs" "$HERE/lib/"
cp "$DL/extract/BepInEx/core/Mono.Cecil.dll" "$HERE/lib/"
sed -e 's/^executable_name=""/executable_name="Night in the Woods.x86_64"/' \
    -e 's#^export DOORSTOP_INVOKE_DLL_PATH=.*#export DOORSTOP_INVOKE_DLL_PATH="$BASEDIR/NITWThai/NITWThai.Loader.dll"#' \
    -e 's/BepInEx start script/NITW Thai mod start script (UnityDoorstop)/' \
    "$DL/extract/run_bepinex.sh" > "$HERE/run_nitwthai.sh"
chmod +x "$HERE/run_nitwthai.sh"
grep -q 'NITWThai/NITWThai.Loader.dll' "$HERE/run_nitwthai.sh" || { echo "launcher adaptation failed" >&2; exit 1; }
# Fonts (SIL Open Font License) from google/fonts. The Noto Serif Thai from
# google/fonts includes Latin digits/quotes the poem uses; notofonts' does not.
mkdir -p "$HERE/fonts"
fetch_font() {
    [ -f "$HERE/fonts/$1" ] || { echo "downloading $1"; curl -fsSL -o "$HERE/fonts/$1.part" "$2" && mv "$HERE/fonts/$1.part" "$HERE/fonts/$1"; }
}
fetch_font Kanit-Medium.ttf https://github.com/google/fonts/raw/main/ofl/kanit/Kanit-Medium.ttf
fetch_font Kanit-OFL.txt https://github.com/google/fonts/raw/main/ofl/kanit/OFL.txt
fetch_font NotoSerifThai.ttf 'https://github.com/google/fonts/raw/main/ofl/notoserifthai/NotoSerifThai%5Bwdth%2Cwght%5D.ttf'
fetch_font NotoSerifThai-OFL.txt https://github.com/google/fonts/raw/main/ofl/notoserifthai/OFL.txt
echo "ready: lib/doorstop_libs lib/Mono.Cecil.dll run_nitwthai.sh fonts/"

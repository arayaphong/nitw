#!/bin/sh
# Build the NITW Thai mod (net35 for the game's Unity 5.6 Mono) with the Roslyn
# compiler bundled in the Unity editor (set UNITY_EDITOR to its Editor folder).
# The game folder is read only, for its managed DLLs as compile references.
# Usage: ./build.sh <game folder containing "Night in the Woods_Data">
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
GAME=${1:?game folder}
UNITY=${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.3.28f1/Editor}
DOTNET=$UNITY/Data/NetCoreRuntime/dotnet
CSC=$UNITY/Data/DotNetSdkRoslyn/csc.dll
OUT=$HERE/out
REF=$OUT/ref
mkdir -p "$REF"
# The game path contains spaces; compile against space-free copies.
for dll in mscorlib System System.Core UnityEngine Assembly-CSharp; do
    cp "$GAME/Night in the Woods_Data/Managed/$dll.dll" "$REF/"
done
cp "$HERE/lib/Mono.Cecil.dll" "$REF/"
common="-nostdlib -noconfig -optimize -langversion:7.3 -nologo -warn:4 -reference:$REF/mscorlib.dll -reference:$REF/System.dll -reference:$REF/System.Core.dll"
"$DOTNET" "$CSC" $common -target:library -out:"$OUT/NITWThai.Runtime.dll" -reference:"$REF/UnityEngine.dll" -reference:"$REF/Assembly-CSharp.dll" "$HERE"/src/Runtime/*.cs
"$DOTNET" "$CSC" $common -target:library -out:"$OUT/NITWThai.Loader.dll" -reference:"$REF/Mono.Cecil.dll" "$HERE"/src/Loader/*.cs
echo "built $OUT/NITWThai.Loader.dll and NITWThai.Runtime.dll (install with ./install.sh)"

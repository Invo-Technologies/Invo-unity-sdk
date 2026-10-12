#!/usr/bin/env bash
# Imports the package's Demo sample into a harness project the way Package Manager does
# (a copy under Assets/Samples/Invo SDK/<version>/Demo), plus the harness scripts and TMP essentials.
# Usage: Tools/Harness/import-sample.sh <harness project dir>
set -eu
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
H="$1"
VER=$(python -I -c "import json,sys;print(json.load(open(sys.argv[1]))['version'])" "$(cygpath -w "$REPO/Packages/com.invonetwork.unity-sdk/package.json")")
DST="$H/Assets/Samples/Invo SDK/$VER/Demo"
rm -rf "$H/Assets/Samples/Invo SDK"
mkdir -p "$DST"
cp -r "$REPO/Packages/com.invonetwork.unity-sdk/Samples~/Demo/." "$DST/"
mkdir -p "$H/Assets/Editor/InvoHarness"
rm -f "$H"/Assets/Editor/InvoHarness/*.cs "$H"/Assets/Editor/InvoHarness/*.cs.meta
cp "$REPO"/Tools/Harness/Editor/*.cs "$H/Assets/Editor/InvoHarness/"
if [ ! -d "$H/Assets/TextMesh Pro" ]; then
  PKG=$(ls -d "$H"/Library/PackageCache/com.unity.textmeshpro@* "$H"/Library/PackageCache/com.unity.ugui@* 2>/dev/null \
        | while read -r d; do [ -f "$d/Package Resources/TMP Essential Resources.unitypackage" ] && echo "$d"; done | head -n 1)
  python -I "$(cygpath -w "$REPO/Tools/Harness/unitypackage_extract.py")" \
    "$(cygpath -w "$PKG/Package Resources/TMP Essential Resources.unitypackage")" "$(cygpath -w "$H")"
fi
echo "sample imported: $DST"

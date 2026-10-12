#!/usr/bin/env bash
# Copies the harness editor scripts and the demo inputs into a harness project and extracts
# TMP Essential Resources into it. Harness projects live outside the repo and are never committed.
# The harness project must have been opened once (so Library/PackageCache exists).
# Usage: Tools/Harness/setup.sh <harness project dir>
set -eu
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
H="$1"

mkdir -p "$H/Assets/Editor/InvoHarness" "$H/Assets/InvoSDK/Resources"
rm -f "$H"/Assets/Editor/InvoHarness/*.cs "$H"/Assets/Editor/InvoHarness/*.cs.meta
cp "$REPO"/Tools/Harness/Editor/*.cs "$H/Assets/Editor/InvoHarness/"
mkdir -p "$H/Assets/InvoDemoBuild/Scripts"
cp "$REPO/Packages/com.invonetwork.unity-sdk/Samples~/Demo/Scripts/InvoCaptureDriver.cs"    "$REPO/Packages/com.invonetwork.unity-sdk/Samples~/Demo/Scripts/InvoCaptureDriver.cs.meta" "$H/Assets/InvoDemoBuild/Scripts/"
for a in InvoSDKItemCatalog.asset InvoSDKWindowConfig.asset; do
  cp "$REPO/Assets/InvoSDK/Resources/$a" "$REPO/Assets/InvoSDK/Resources/$a.meta" "$H/Assets/InvoSDK/Resources/"
done

# TMP Essential Resources: in com.unity.textmeshpro on 2021.3/2022.3, in com.unity.ugui on 2023.2+.
if [ ! -d "$H/Assets/TextMesh Pro" ]; then
  PKG=$(ls -d "$H"/Library/PackageCache/com.unity.textmeshpro@* "$H"/Library/PackageCache/com.unity.ugui@* 2>/dev/null \
        | while read -r d; do [ -f "$d/Package Resources/TMP Essential Resources.unitypackage" ] && echo "$d"; done | head -n 1)
  python -I "$(cygpath -w "$REPO/Tools/Harness/unitypackage_extract.py")" \
    "$(cygpath -w "$PKG/Package Resources/TMP Essential Resources.unitypackage")" "$(cygpath -w "$H")"
fi
echo "harness ready: $H"

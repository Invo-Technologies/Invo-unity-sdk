#!/usr/bin/env bash
# Regenerates the shared UI assets in Unity 2021.3: the Inter SDF font asset, the panel and window
# prefabs, and the demo scene (exported to the package's Samples~/Demo). Run from any directory.
# Usage: Tools/Harness/build-ui.sh <2021.3 harness dir> <log dir>
set -eu
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
H="$1"; LOGS="$2"
U="/c/Program Files/Unity/Hub/Editor/2021.3.45f2/Editor/Unity.exe"
PKG="$REPO/Packages/com.invonetwork.unity-sdk"
FONT="$PKG/Runtime/Art/Inter/Inter-VariableFont_slnt,wght SDF.asset"
HW="$(cygpath -w "$H")"; LW="$(cygpath -w "$LOGS")"

run() { # run <method> <log>
  "$U" -batchmode -nographics -quit -projectPath "$HW" -executeMethod "InvoSDK.Harness.InvoUiKitBuilder.$1" -logFile "$LW\\$2"
  local rc=$?
  grep -E "\[InvoUi" "$LOGS/$2" | grep -v "Saved Packages" || true
  [ $rc -eq 0 ] || { echo "$1 failed (exit $rc), see $LOGS/$2"; exit $rc; }
}

bash "$REPO/Tools/Harness/setup.sh" "$H"

# 1. Font: regenerate in 2021.3, then give the material and atlas their original local IDs so every
#    TMP component's m_sharedMaterial reference keeps resolving.
if grep -q "m_IgnoreMipmapLimit" "$FONT" || grep -qE "^  material: \{fileID: 0\}" "$FONT"; then
run RegenerateFont ui-1-font.log
python -I - "$(cygpath -w "$FONT")" <<'PY'
import re, sys
p = sys.argv[1]
s = open(p, encoding='utf-8').read()
ids = {}
for cls, fid in re.findall(r'^--- !u!(\d+) &(-?\d+)', s, flags=re.M):
    ids.setdefault(cls, fid)
mapping = {ids['21']: '-5516924611146526891', ids['28']: '-7962225304746942516'}
for old, new in mapping.items():
    s = re.sub(r'(?<![\d-])' + re.escape(old) + r'(?!\d)', new, s)
open(p, 'w', encoding='utf-8', newline='').write(s)
print('font ids remapped:', mapping)
PY
else
  echo "font asset already in 2021.3 format; not regenerated"
fi
grep -E "^  material:" "$FONT"

# 2. Theme assets, item card, panel, window and API prefabs.  3. Demo scene (separate run: 2021.3 can crash otherwise).
run Build ui-2-prefabs.log
run BuildDemo ui-3-demo.log

# 4. Export the demo to Samples~/Demo (scene, sample Resources, capture driver).
D="$PKG/Samples~/Demo"
mkdir -p "$D/Resources"
cp "$H/Assets/InvoDemoBuild/InvoDemo.unity" "$H/Assets/InvoDemoBuild/InvoDemo.unity.meta" "$D/"
cp "$H/Assets/InvoDemoBuild/Resources/"*.asset "$H/Assets/InvoDemoBuild/Resources/"*.asset.meta "$D/Resources/"
cp "$H/Assets/InvoDemoBuild/Resources.meta" "$D/Resources.meta"
echo "demo exported to $D"

# 5. The repo root's Unity 6 sample project uses the same scene (keeps MainScene's GUID).
cp "$H/Assets/InvoDemoBuild/RootMainScene.unity" "$REPO/Assets/Scenes/MainScene.unity"
echo "root MainScene updated"

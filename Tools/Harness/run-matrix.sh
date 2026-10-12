#!/usr/bin/env bash
# One row of the test matrix: import the Demo sample, compile, EditMode tests, Windows build,
# screenshot capture, WebGL build. Logs, test results and screenshots go to <out>/<name>/.
# Usage: Tools/Harness/run-matrix.sh <editor version> <harness dir> <out dir>
set -u
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
VER="$1"; H="$2"; OUT="$3"; NAME="$(basename "$H")"
U="/c/Program Files/Unity/Hub/Editor/$VER/Editor/Unity.exe"
O="$OUT/$NAME"; mkdir -p "$O/shots"
HW="$(cygpath -w "$H")"; OW="$(cygpath -w "$O")"
row() { echo "RESULT $NAME $1 $2"; }

# Unity 6's TextMeshPro re-saves the shared font asset in a format 2021.3/2022.3 cannot read
# (no material), so every row starts from the committed 2021.3 copy.
git -C "$REPO" checkout -- "Packages/com.invonetwork.unity-sdk/Runtime/Art/Inter/Inter-VariableFont_slnt,wght SDF.asset"

bash "$REPO/Tools/Harness/import-sample.sh" "$H" >/dev/null || { row import fail; exit 1; }

"$U" -batchmode -nographics -quit -projectPath "$HW" -logFile "$OW\compile.log"
errs=$(grep -c "error CS" "$O/compile.log"); warns=$(grep -E "warning CS" "$O/compile.log" | grep -ci "invonetwork\|InvoDemo\|InvoHarness")
[ "$errs" = 0 ] && row compile "pass (warnings in SDK/sample: $warns)" || { row compile "fail ($errs errors)"; exit 1; }

"$U" -batchmode -nographics -projectPath "$HW" -runTests -testPlatform EditMode -assemblyNames InvoSDK.Tests.Editor \
  -testResults "$OW\tests.xml" -logFile "$OW\tests.log"
row tests "$(grep -oE '<test-run [^>]*' "$O/tests.xml" | grep -oE '(result|total|passed|failed)="[^"]*"' | tr '\n' ' ')"

"$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessBuild.Windows \
  -invoOut "$OW\win" -logFile "$OW\build-win.log"
[ $? = 0 ] && row windows "pass $(grep -oE '[0-9]+ MB' "$O/build-win.log" | tail -1)" || row windows fail

if [ -f "$O/win/InvoDemo.exe" ]; then
  timeout 600 "$O/win/InvoDemo.exe" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
    -invoCapture "$OW\shots" -logFile "$OW\capture.log" >/dev/null
  row capture "$(grep -oE '\[InvoCapture\] finished, [0-9]+ failure\(s\)' "$O/capture.log" || echo 'did not finish') $(ls "$O/shots" | wc -l) files"
fi

"$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessBuild.WebGL \
  -invoOut "$OW\webgl" -logFile "$OW\build-webgl.log"
[ $? = 0 ] && row webgl "pass $(grep -oE '[0-9]+ MB' "$O/build-webgl.log" | tail -1)" || row webgl fail

# Mobile builds, when the editor has the module: an Android APK and an iOS Xcode project
# (compiling the Xcode project needs a Mac).
PE="$(dirname "$U")/Data/PlaybackEngines"
if [ -d "$PE/AndroidPlayer" ]; then
  "$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessBuild.Android \
    -invoOut "$OW\android" -logFile "$OW\build-android.log"
  [ $? = 0 ] && row android "pass $(grep -oE '[0-9]+ MB' "$O/build-android.log" | tail -1)" || row android fail
else
  row android "not run (module not installed)"
fi
if [ -d "$PE/iOSSupport" ]; then
  "$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessBuild.iOS \
    -invoOut "$OW\ios" -logFile "$OW\build-ios.log"
  [ $? = 0 ] && row ios "pass (Xcode project exported)" || row ios fail
else
  row ios "not run (module not installed)"
fi

# Sandbox smoke test: only when INVO_SANDBOX_GAME_ID and the INVO_SANDBOX_KEY_* variables are set.
# The keys go into the harness project's config for one Windows build and are removed afterwards.
if [ -n "${INVO_SANDBOX_GAME_ID:-}" ]; then
  "$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessSandbox.Configure -logFile "$OW\sandbox-config.log"
  "$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessBuild.Windows \
    -invoOut "$OW\win-smoke" -logFile "$OW\build-win-smoke.log"
  "$U" -batchmode -quit -projectPath "$HW" -executeMethod InvoSDK.Harness.InvoHarnessSandbox.Strip -logFile "$OW\sandbox-strip.log"
  if [ -f "$O/win-smoke/InvoDemo.exe" ]; then
    timeout 300 "$O/win-smoke/InvoDemo.exe" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
      -invoSmoke "$OW\smoke.txt" -logFile "$OW\smoke.log" >/dev/null
    row smoke "$(tail -1 "$O/smoke.txt" 2>/dev/null || echo 'did not finish')"
    rm -rf "$O/win-smoke"   # the build carries the sandbox key
  else
    row smoke "build failed"
  fi
fi

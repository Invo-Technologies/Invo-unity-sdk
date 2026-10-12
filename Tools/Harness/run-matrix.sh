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

#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_PROJECT_PATH="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
UNITY_PROJECT_PATH="${BALANCY_UNITY_PROJECT_PATH:-$DEFAULT_PROJECT_PATH}"
UNITY_PATH="${UNITY_PATH:-}"
PLATFORM="${BALANCY_IOS_SMOKE_PLATFORM:-all}"

if [[ -z "$UNITY_PATH" ]]; then
  UNITY_VERSION="$(awk '/m_EditorVersion:/{print $2; exit}' "$UNITY_PROJECT_PATH/ProjectSettings/ProjectVersion.txt")"
  for candidate in \
    "/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity" \
    /Volumes/*/Applications/Unity/Hub/Editor/"$UNITY_VERSION"/Unity.app/Contents/MacOS/Unity; do
    if [[ -x "$candidate" ]]; then
      UNITY_PATH="$candidate"
      break
    fi
  done
fi

if [[ ! -x "$UNITY_PATH" ]]; then
  echo "Unity executable was not found. Set UNITY_PATH explicitly."
  exit 2
fi

if [[ "$PLATFORM" == "all" ]]; then
  for platform in simulator device; do
    echo "Running iOS $platform link smoke test..."
    BALANCY_IOS_SMOKE_PLATFORM="$platform" "$0"
  done
  echo "Unity iOS simulator and device link smoke tests succeeded."
  exit 0
fi

WORK_DIR="${BALANCY_IOS_SMOKE_DIR:-$(mktemp -d /tmp/balancy-ios-link-smoke.XXXXXX)}"
XCODE_PATH="$WORK_DIR/Xcode"
UNITY_LOG="$WORK_DIR/unity-export.log"
XCODE_LOG="$WORK_DIR/xcodebuild.log"

case "$PLATFORM" in
  simulator)
    EXPORT_METHOD="Balancy.Editor.BalancyIOSLinkSmokeBuild.ExportSimulator"
    XCODE_SDK="iphonesimulator"
    XCODE_DESTINATION="generic/platform=iOS Simulator"
    ;;
  device)
    EXPORT_METHOD="Balancy.Editor.BalancyIOSLinkSmokeBuild.ExportDevice"
    XCODE_SDK="iphoneos"
    XCODE_DESTINATION="generic/platform=iOS"
    ;;
  *)
    echo "BALANCY_IOS_SMOKE_PLATFORM must be 'simulator' or 'device'."
    exit 2
    ;;
esac

cleanup() {
  if [[ "${BALANCY_KEEP_IOS_SMOKE_OUTPUT:-0}" != "1" ]]; then
    rm -rf "$WORK_DIR"
  else
    echo "Kept smoke-test output at: $WORK_DIR"
  fi
}
trap cleanup EXIT

export BALANCY_IOS_BUILD_PATH="$XCODE_PATH"

if ! "$UNITY_PATH" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$UNITY_PROJECT_PATH" \
  -buildTarget iOS \
  -executeMethod "$EXPORT_METHOD" \
  -logFile "$UNITY_LOG"; then
  tail -200 "$UNITY_LOG"
  exit 1
fi

if ! rg -q 'BalancyCore\.xcframework' "$XCODE_PATH/Unity-iPhone.xcodeproj/project.pbxproj"; then
  echo "BalancyCore.xcframework is missing from the generated Xcode project."
  exit 1
fi

xcodebuild \
  -project "$XCODE_PATH/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone \
  -configuration Debug \
  -sdk "$XCODE_SDK" \
  -destination "$XCODE_DESTINATION" \
  -derivedDataPath "$WORK_DIR/DerivedData" \
  CODE_SIGNING_ALLOWED=NO \
  build | tee "$XCODE_LOG"

if rg -q 'Undefined symbols for architecture|Undefined symbol:' "$XCODE_LOG"; then
  echo "Native iOS link failed with undefined symbols."
  exit 1
fi

echo "Unity iOS $PLATFORM export and final Xcode link succeeded."

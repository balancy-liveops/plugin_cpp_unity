#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNITY_PROJECT_PATH="${BALANCY_UNITY_PROJECT_PATH:-$(cd "$SCRIPT_DIR/../../../.." && pwd)}"
UNITY_PATH="${UNITY_PATH:-}"

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

WORK_DIR="${BALANCY_WEBGL_SMOKE_DIR:-$(mktemp -d /tmp/balancy-webgl-link-smoke.XXXXXX)}"
PLAYER_PATH="$WORK_DIR/Player"
UNITY_LOG="$WORK_DIR/unity-build.log"

cleanup() {
  if [[ "${BALANCY_KEEP_WEBGL_SMOKE_OUTPUT:-0}" != "1" ]]; then
    rm -rf "$WORK_DIR"
  else
    echo "Kept smoke-test output at: $WORK_DIR"
  fi
}
trap cleanup EXIT

export BALANCY_WEBGL_BUILD_PATH="$PLAYER_PATH"

if ! "$UNITY_PATH" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$UNITY_PROJECT_PATH" \
  -buildTarget WebGL \
  -executeMethod Balancy.Editor.BalancyWebGLLinkSmokeBuild.Build \
  -logFile "$UNITY_LOG"; then
  tail -200 "$UNITY_LOG"
  exit 1
fi

if [[ ! -f "$PLAYER_PATH/index.html" ]]; then
  echo "Unity WebGL player output is missing index.html."
  exit 1
fi

echo "Unity WebGL player build and native link succeeded."

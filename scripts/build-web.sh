#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")"
DEFAULT_UNITY_PATH="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
UNITY_EXECUTABLE="${UNITY_PATH:-$DEFAULT_UNITY_PATH}"
EDITOR_ROOT="$(cd "$(dirname "$UNITY_EXECUTABLE")/../../.." 2>/dev/null && pwd || true)"
WEBGL_SUPPORT="$EDITOR_ROOT/PlaybackEngines/WebGLSupport"
LOG_PATH="$PROJECT_ROOT/Logs/WebGLBuild.log"

if [[ ! -x "$UNITY_EXECUTABLE" ]]; then
    echo "Unity $UNITY_VERSION was not found at:"
    echo "  $UNITY_EXECUTABLE"
    echo "Set UNITY_PATH to the Unity executable if it is installed elsewhere."
    exit 1
fi

if [[ ! -d "$WEBGL_SUPPORT" ]]; then
    echo "Unity WebGL Build Support is not installed for Unity $UNITY_VERSION."
    echo "Add it from Unity Hub > Installs > $UNITY_VERSION > Add modules."
    exit 1
fi

if [[ -e "$PROJECT_ROOT/Temp/UnityLockfile" ]]; then
    echo "This project appears to be open in Unity."
    echo "Close the editor before using this script, or use MadFact > Build > WebGL Export inside Unity."
    exit 2
fi

mkdir -p "$(dirname "$LOG_PATH")"

echo "Building MadMovieFact WebGL with Unity $UNITY_VERSION..."
if "$UNITY_EXECUTABLE" \
    -batchmode \
    -nographics \
    -quit \
    -projectPath "$PROJECT_ROOT" \
    -buildTarget WebGL \
    -executeMethod WebBuildExporter.BuildFromCommandLine \
    -logFile "$LOG_PATH"; then
    :
else
    status=$?
    echo "WebGL build failed. Last Unity log lines:"
    tail -n 80 "$LOG_PATH" || true
    exit "$status"
fi

echo "WebGL export created:"
echo "  $PROJECT_ROOT/Builds/WebGL"
echo "  $PROJECT_ROOT/Builds/MadMovieFact-WebGL.zip"
echo "Unity log: $LOG_PATH"

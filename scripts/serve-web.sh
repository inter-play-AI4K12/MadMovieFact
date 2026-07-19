#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")"
DEFAULT_UNITY_PATH="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
UNITY_EXECUTABLE="${UNITY_PATH:-$DEFAULT_UNITY_PATH}"
EDITOR_ROOT="$(cd "$(dirname "$UNITY_EXECUTABLE")/../../.." 2>/dev/null && pwd || true)"
MONO_EXECUTABLE="$EDITOR_ROOT/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge/bin/mono"
SERVER_EXECUTABLE="$EDITOR_ROOT/PlaybackEngines/WebGLSupport/BuildTools/SimpleWebServer.exe"
BUILD_DIRECTORY="$PROJECT_ROOT/Builds/WebGL"
PORT="${PORT:-8080}"
URL="http://localhost:$PORT/"

if [[ ! -f "$BUILD_DIRECTORY/index.html" ]]; then
    echo "No WebGL export was found at $BUILD_DIRECTORY."
    echo "Run ./scripts/build-web.sh first."
    exit 1
fi

if [[ ! -x "$MONO_EXECUTABLE" || ! -f "$SERVER_EXECUTABLE" ]]; then
    echo "Unity's WebGL server was not found for Unity $UNITY_VERSION."
    echo "Check UNITY_PATH and confirm WebGL Build Support is installed."
    exit 1
fi

echo "Serving MadMovieFact at $URL"
echo "Press Ctrl+C to stop."
exec "$MONO_EXECUTABLE" "$SERVER_EXECUTABLE" "$BUILD_DIRECTORY" "$URL"

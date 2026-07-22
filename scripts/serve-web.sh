#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PORT="${PORT:-8081}"
HOST="${HOST:-0.0.0.0}"

if [[ -f "$SCRIPT_DIR/index.html" ]]; then
    BUILD_DIRECTORY="$SCRIPT_DIR"
    SERVER_SCRIPT="$SCRIPT_DIR/serve_web.py"
else
    PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
    BUILD_DIRECTORY="$PROJECT_ROOT/Builds/WebGL"
    SERVER_SCRIPT="$SCRIPT_DIR/serve_web.py"
fi

if [[ ! -f "$BUILD_DIRECTORY/index.html" ]]; then
    echo "No WebGL export was found at $BUILD_DIRECTORY."
    echo "Run ./scripts/build-web.sh first."
    exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
    echo "Python 3 is required to run the local WebGL telemetry relay."
    exit 1
fi

exec python3 "$SERVER_SCRIPT" \
    --host "$HOST" \
    --port "$PORT" \
    --directory "$BUILD_DIRECTORY"

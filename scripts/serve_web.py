#!/usr/bin/env python3
"""Serve the WebGL export and relay consented telemetry to Loki.

The browser posts to a same-origin endpoint so the Loki password never enters the
WebGL build and Loki does not need to allow browser CORS requests.
"""

from __future__ import annotations

import argparse
import base64
from http import HTTPStatus
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import ssl
from typing import Any
from urllib import error, parse, request

try:
    import certifi
except ImportError:
    certifi = None


DEFAULT_LOKI_ENDPOINT = (
    "https://loki-madfact.interplaylab.io/loki/api/v1/push"
)
MAX_TELEMETRY_BYTES = 64 * 1024
TELEMETRY_PATH = "/api/telemetry"


def load_dotenv(path: Path) -> None:
    if not path.is_file():
        return
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.removeprefix("export ").strip()
        value = value.strip().strip("\"'")
        os.environ.setdefault(key, value)


def loki_endpoint() -> str:
    explicit = os.getenv("LOKI_ENDPOINT", "").strip()
    if explicit:
        return explicit
    base = os.getenv("LOKI_URL", "").strip().rstrip("/")
    return f"{base}/loki/api/v1/push" if base else DEFAULT_LOKI_ENDPOINT


def tls_context() -> ssl.SSLContext:
    if certifi is not None:
        return ssl.create_default_context(cafile=certifi.where())
    return ssl.create_default_context()


def validate_loki_payload(payload: Any) -> str | None:
    if not isinstance(payload, dict):
        return "Telemetry payload must be a JSON object."
    streams = payload.get("streams")
    if not isinstance(streams, list) or len(streams) != 1:
        return "Telemetry payload must contain exactly one stream."

    stream = streams[0]
    if not isinstance(stream, dict):
        return "Telemetry stream is invalid."
    labels = stream.get("stream")
    if labels != {"app": "madfact", "source": "unity"}:
        return "Telemetry stream labels are not allowed."

    values = stream.get("values")
    if not isinstance(values, list) or not 1 <= len(values) <= 20:
        return "Telemetry payload must contain between 1 and 20 values."

    for value in values:
        if (
            not isinstance(value, list)
            or len(value) != 2
            or not isinstance(value[0], str)
            or not value[0].isdigit()
            or not isinstance(value[1], str)
        ):
            return "Telemetry value format is invalid."
        if len(value[1].encode("utf-8")) > 48 * 1024:
            return "Telemetry record is too large."
        try:
            record = json.loads(value[1])
        except json.JSONDecodeError:
            return "Telemetry record must contain JSON."
        if not isinstance(record, dict):
            return "Telemetry record must be a JSON object."
        if record.get("app") != "madfact" or record.get("source") != "unity":
            return "Telemetry record identity is invalid."
        if not isinstance(record.get("event_type"), str):
            return "Telemetry event type is missing."
        if not isinstance(record.get("game_session_id"), str):
            return "Telemetry session ID is missing."
        if not isinstance(record.get("participant_id"), str):
            return "Telemetry participant ID is missing."

    return None


class MadFactWebHandler(SimpleHTTPRequestHandler):
    server_version = "MadFactWeb/1.0"

    def do_POST(self) -> None:
        if parse.urlparse(self.path).path != TELEMETRY_PATH:
            self.send_error(HTTPStatus.NOT_FOUND)
            return

        try:
            content_length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            content_length = 0
        if content_length <= 0 or content_length > MAX_TELEMETRY_BYTES:
            self._write_json(
                HTTPStatus.REQUEST_ENTITY_TOO_LARGE,
                {"error": "Telemetry payload size is invalid."},
            )
            return

        body = self.rfile.read(content_length)
        try:
            payload = json.loads(body.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            self._write_json(
                HTTPStatus.BAD_REQUEST,
                {"error": "Telemetry payload is not valid JSON."},
            )
            return

        validation_error = validate_loki_payload(payload)
        if validation_error:
            self._write_json(
                HTTPStatus.BAD_REQUEST,
                {"error": validation_error},
            )
            return

        password = os.getenv("LOKI_PASSWORD", "").strip()
        if not password:
            self._write_json(
                HTTPStatus.SERVICE_UNAVAILABLE,
                {
                    "error": (
                        "LOKI_PASSWORD is not configured on the local web server."
                    )
                },
            )
            return

        username = os.getenv("LOKI_USER", "beetrap").strip() or "beetrap"
        credentials = base64.b64encode(
            f"{username}:{password}".encode("utf-8")
        ).decode("ascii")
        upstream = request.Request(
            loki_endpoint(),
            data=body,
            method="POST",
            headers={
                "Authorization": f"Basic {credentials}",
                "Content-Type": "application/json",
                "User-Agent": "MadFactWeb/1.0",
            },
        )

        try:
            with request.urlopen(
                upstream,
                timeout=15,
                context=tls_context(),
            ) as response:
                response.read()
                if 200 <= response.status < 300:
                    self.send_response(HTTPStatus.NO_CONTENT)
                    self.send_header("Cache-Control", "no-store")
                    self.end_headers()
                    return
                status = response.status
        except error.HTTPError as exception:
            status = exception.code
            exception.read()
        except (error.URLError, TimeoutError) as exception:
            self._write_json(
                HTTPStatus.BAD_GATEWAY,
                {
                    "error": "Could not reach Loki.",
                    "detail": type(exception).__name__,
                },
            )
            return

        self._write_json(
            HTTPStatus.BAD_GATEWAY,
            {"error": f"Loki returned HTTP {status}."},
        )

    def guess_type(self, path: str) -> str:
        if path.endswith(".gz"):
            path = path[:-3]
        elif path.endswith(".br"):
            path = path[:-3]
        return super().guess_type(path)

    def end_headers(self) -> None:
        path = parse.urlparse(self.path).path
        if path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        elif path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        if path != TELEMETRY_PATH:
            self.send_header("Cache-Control", "no-cache")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cross-Origin-Resource-Policy", "same-origin")
        super().end_headers()

    def _write_json(self, status: HTTPStatus, payload: dict[str, Any]) -> None:
        body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format_string: str, *args: Any) -> None:
        print(f"[madfact-web] {self.address_string()} {format_string % args}")


def default_build_directory() -> Path:
    script_directory = Path(__file__).resolve().parent
    if (script_directory / "index.html").is_file():
        return script_directory
    return script_directory.parent / "Builds" / "WebGL"


def load_local_configuration(
    build_directory: Path, explicit_env_file: Path | None
) -> Path | None:
    script_directory = Path(__file__).resolve().parent
    candidates = [
        explicit_env_file,
        build_directory / ".env",
        script_directory / ".env",
        script_directory.parent / ".env",
    ]
    for candidate in candidates:
        if candidate is not None and candidate.is_file():
            load_dotenv(candidate)
            return candidate
    return None


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Serve MadMovieFact WebGL with a local Loki telemetry relay."
    )
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=int(os.getenv("PORT", "8080")))
    parser.add_argument("--directory", type=Path, default=default_build_directory())
    parser.add_argument("--env-file", type=Path)
    args = parser.parse_args()

    build_directory = args.directory.resolve()
    if not (build_directory / "index.html").is_file():
        raise SystemExit(f"No WebGL export was found at {build_directory}.")

    env_path = load_local_configuration(build_directory, args.env_file)
    configured = bool(os.getenv("LOKI_PASSWORD", "").strip())
    handler = lambda *handler_args, **handler_kwargs: MadFactWebHandler(
        *handler_args,
        directory=str(build_directory),
        **handler_kwargs,
    )
    server = ThreadingHTTPServer((args.host, args.port), handler)

    print(f"Serving MadMovieFact at http://localhost:{args.port}/")
    print(
        "Telemetry relay: "
        + ("configured" if configured else "disabled (LOKI_PASSWORD is missing)")
    )
    if env_path is not None:
        print(f"Configuration: {env_path}")
    print("Open this machine's LAN address from other devices. Press Ctrl+C to stop.")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping MadMovieFact web server.")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Serve the WebGL export, relay telemetry, and generate Level 8 posters.

The browser posts to same-origin endpoints so Loki and OpenAI credentials never
enter the WebGL build.
"""

from __future__ import annotations

import argparse
import base64
from http import HTTPStatus
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import ssl
import threading
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
POSTER_PATH = "/api/poster/generate"
MAX_POSTER_REQUEST_BYTES = 16 * 1024
MAX_POSTER_PROMPT_CHARACTERS = 4000
MAX_POSTERS_PER_SESSION = 7
OPENAI_IMAGE_ENDPOINT = "https://api.openai.com/v1/images/generations"
POSTER_SESSION_PATTERN = re.compile(r"^[A-Za-z0-9_-]{8,128}$")
POSTER_GENERATION_COUNTS: dict[str, int] = {}
POSTER_GENERATION_LOCK = threading.Lock()

POSTER_SAFETY_PREFIX = (
    "Create fictional, family-friendly movie-poster artwork appropriate for "
    "sixth-grade students. Do not depict gore, graphic violence, sexual content, "
    "drugs, hateful imagery, or real-person likenesses. "
)


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


def validate_poster_payload(payload: Any) -> str | None:
    if not isinstance(payload, dict):
        return "Poster request must be a JSON object."
    session_id = payload.get("session_id")
    if not isinstance(session_id, str) or not POSTER_SESSION_PATTERN.fullmatch(
        session_id
    ):
        return "Poster session ID is invalid."
    prompt = payload.get("prompt")
    if not isinstance(prompt, str):
        return "Poster prompt is missing."
    prompt = prompt.strip()
    if len(prompt) < 20:
        return "Poster prompt needs more detail."
    if len(prompt) > MAX_POSTER_PROMPT_CHARACTERS:
        return "Poster prompt is too long."
    if "\x00" in prompt:
        return "Poster prompt contains invalid text."
    return None


def reserve_poster_slot(session_id: str) -> int | None:
    """Reserve one of seven in-process slots; return remaining or None at the cap."""
    with POSTER_GENERATION_LOCK:
        count = POSTER_GENERATION_COUNTS.get(session_id, 0)
        if count >= MAX_POSTERS_PER_SESSION:
            return None
        count += 1
        POSTER_GENERATION_COUNTS[session_id] = count
        return MAX_POSTERS_PER_SESSION - count


def release_poster_slot(session_id: str) -> None:
    with POSTER_GENERATION_LOCK:
        count = POSTER_GENERATION_COUNTS.get(session_id, 0)
        if count <= 1:
            POSTER_GENERATION_COUNTS.pop(session_id, None)
        else:
            POSTER_GENERATION_COUNTS[session_id] = count - 1


class MadFactWebHandler(SimpleHTTPRequestHandler):
    server_version = "MadFactWeb/1.0"

    def do_POST(self) -> None:
        path = parse.urlparse(self.path).path
        if path == POSTER_PATH:
            self._generate_poster()
            return
        if path != TELEMETRY_PATH:
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

    def _generate_poster(self) -> None:
        try:
            content_length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            content_length = 0
        if content_length <= 0 or content_length > MAX_POSTER_REQUEST_BYTES:
            self._write_json(
                HTTPStatus.REQUEST_ENTITY_TOO_LARGE,
                {"error": "Poster request size is invalid."},
            )
            return

        body = self.rfile.read(content_length)
        try:
            payload = json.loads(body.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            self._write_json(
                HTTPStatus.BAD_REQUEST,
                {"error": "Poster request is not valid JSON."},
            )
            return

        validation_error = validate_poster_payload(payload)
        if validation_error:
            self._write_json(
                HTTPStatus.BAD_REQUEST,
                {"error": validation_error},
            )
            return

        api_key = os.getenv("OPENAI_API_KEY", "").strip()
        if not api_key:
            self._write_json(
                HTTPStatus.SERVICE_UNAVAILABLE,
                {
                    "error": (
                        "Poster generation is not configured. Add OPENAI_API_KEY "
                        "to the local server's .env file."
                    ),
                    "code": "poster_not_configured",
                },
            )
            return

        session_id = payload["session_id"]
        remaining = reserve_poster_slot(session_id)
        if remaining is None:
            self._write_json(
                HTTPStatus.TOO_MANY_REQUESTS,
                {
                    "error": "This game session has already generated seven posters.",
                    "code": "generation_limit",
                },
            )
            return

        upstream_payload = json.dumps(
            {
                "model": "gpt-image-2",
                "prompt": POSTER_SAFETY_PREFIX + payload["prompt"].strip(),
                "size": "1024x1536",
                "quality": "medium",
                "output_format": "jpeg",
                "output_compression": 85,
                "moderation": "auto",
                "n": 1,
            },
            separators=(",", ":"),
        ).encode("utf-8")
        upstream = request.Request(
            OPENAI_IMAGE_ENDPOINT,
            data=upstream_payload,
            method="POST",
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
                "User-Agent": "MadFactWeb/1.0",
            },
        )

        try:
            with request.urlopen(
                upstream,
                timeout=150,
                context=tls_context(),
            ) as response:
                response_body = response.read()
                if not 200 <= response.status < 300:
                    raise RuntimeError(f"Unexpected image status {response.status}")
            result = json.loads(response_body.decode("utf-8"))
            image_base64 = result["data"][0]["b64_json"]
            if not isinstance(image_base64, str) or not image_base64:
                raise ValueError("Image response did not contain b64_json")
        except error.HTTPError as exception:
            release_poster_slot(session_id)
            upstream_status = exception.code
            upstream_error = None
            upstream_code = ""
            try:
                upstream_error = json.loads(exception.read().decode("utf-8"))
                upstream_code = str(
                    upstream_error.get("error", {}).get("code", "")
                )
            except (UnicodeDecodeError, json.JSONDecodeError, AttributeError):
                pass

            if upstream_code == "moderation_blocked":
                self._write_json(
                    HTTPStatus.BAD_REQUEST,
                    {
                        "error": "That prompt was blocked by image safety checks.",
                        "code": "moderation_blocked",
                    },
                )
            elif upstream_status == HTTPStatus.TOO_MANY_REQUESTS:
                self._write_json(
                    HTTPStatus.TOO_MANY_REQUESTS,
                    {
                        "error": "The image service is busy. Try again shortly.",
                        "code": "upstream_rate_limit",
                    },
                )
            elif upstream_status in (
                HTTPStatus.UNAUTHORIZED,
                HTTPStatus.FORBIDDEN,
            ):
                self._write_json(
                    HTTPStatus.SERVICE_UNAVAILABLE,
                    {
                        "error": "The local poster API credential was rejected.",
                        "code": "credential_rejected",
                    },
                )
            else:
                self._write_json(
                    HTTPStatus.BAD_GATEWAY,
                    {"error": f"The image service returned HTTP {upstream_status}."},
                )
            return
        except (error.URLError, TimeoutError):
            release_poster_slot(session_id)
            self._write_json(
                HTTPStatus.BAD_GATEWAY,
                {"error": "Could not reach the image service."},
            )
            return
        except (KeyError, IndexError, TypeError, ValueError, RuntimeError):
            release_poster_slot(session_id)
            self._write_json(
                HTTPStatus.BAD_GATEWAY,
                {"error": "The image service returned an invalid response."},
            )
            return

        self._write_json(
            HTTPStatus.OK,
            {
                "image_base64": image_base64,
                "mime_type": "image/jpeg",
                "remaining": remaining,
            },
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
        if path not in (TELEMETRY_PATH, POSTER_PATH):
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
        description="Serve MadMovieFact WebGL with local telemetry and poster relays."
    )
    parser.add_argument("--host", default="0.0.0.0")
    # Unity's temporary WebGL preview commonly occupies 8080. Keeping the
    # launcher on 8081 prevents API requests from reaching that static server.
    parser.add_argument("--port", type=int, default=int(os.getenv("PORT", "8081")))
    parser.add_argument("--directory", type=Path, default=default_build_directory())
    parser.add_argument("--env-file", type=Path)
    args = parser.parse_args()

    build_directory = args.directory.resolve()
    if not (build_directory / "index.html").is_file():
        raise SystemExit(f"No WebGL export was found at {build_directory}.")

    env_path = load_local_configuration(build_directory, args.env_file)
    configured = bool(os.getenv("LOKI_PASSWORD", "").strip())
    poster_configured = bool(os.getenv("OPENAI_API_KEY", "").strip())
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
    print(
        "Poster generation: "
        + (
            "configured"
            if poster_configured
            else "disabled (OPENAI_API_KEY is missing)"
        )
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

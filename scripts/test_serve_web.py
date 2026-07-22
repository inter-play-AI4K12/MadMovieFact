import json
import unittest

from scripts.serve_web import (
    MAX_POSTERS_PER_SESSION,
    POSTER_GENERATION_COUNTS,
    release_poster_slot,
    reserve_poster_slot,
    validate_loki_payload,
    validate_poster_payload,
)


def valid_payload():
    record = {
        "app": "madfact",
        "source": "unity",
        "event_type": "level_started",
        "game_session_id": "session-1",
        "participant_id": "participant-1",
    }
    return {
        "streams": [
            {
                "stream": {"app": "madfact", "source": "unity"},
                "values": [["123456789", json.dumps(record)]],
            }
        ]
    }


class TelemetryRelayValidationTest(unittest.TestCase):
    def test_accepts_game_payload(self):
        self.assertIsNone(validate_loki_payload(valid_payload()))

    def test_rejects_changed_stream_labels(self):
        payload = valid_payload()
        payload["streams"][0]["stream"]["source"] = "untrusted"
        self.assertEqual(
            validate_loki_payload(payload),
            "Telemetry stream labels are not allowed.",
        )

    def test_rejects_record_without_session(self):
        payload = valid_payload()
        record = json.loads(payload["streams"][0]["values"][0][1])
        del record["game_session_id"]
        payload["streams"][0]["values"][0][1] = json.dumps(record)
        self.assertEqual(
            validate_loki_payload(payload),
            "Telemetry session ID is missing.",
        )


class PosterRelayValidationTest(unittest.TestCase):
    def setUp(self):
        POSTER_GENERATION_COUNTS.clear()

    def test_accepts_level_eight_prompt(self):
        self.assertIsNone(
            validate_poster_payload(
                {
                    "session_id": "poster-session-123",
                    "prompt": "A family-friendly spooky comedy poster in a video store.",
                }
            )
        )

    def test_rejects_short_or_invalid_requests(self):
        self.assertEqual(
            validate_poster_payload(
                {"session_id": "short", "prompt": "A full poster prompt."}
            ),
            "Poster session ID is invalid.",
        )
        self.assertEqual(
            validate_poster_payload(
                {"session_id": "poster-session-123", "prompt": "too short"}
            ),
            "Poster prompt needs more detail.",
        )

    def test_caps_each_session_at_seven_reserved_generations(self):
        session_id = "poster-session-cap"
        for _ in range(MAX_POSTERS_PER_SESSION):
            self.assertIsNotNone(reserve_poster_slot(session_id))
        self.assertIsNone(reserve_poster_slot(session_id))

        release_poster_slot(session_id)
        self.assertEqual(reserve_poster_slot(session_id), 0)


if __name__ == "__main__":
    unittest.main()

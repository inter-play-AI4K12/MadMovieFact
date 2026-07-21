import json
import unittest

from scripts.serve_web import validate_loki_payload


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


if __name__ == "__main__":
    unittest.main()

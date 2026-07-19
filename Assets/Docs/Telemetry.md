# MadFact participant sessions and telemetry

The game starts in `ParticipantSetup.unity`. Remote telemetry is disabled until the
player supplies a display name and explicitly enables the consent toggle. A study ID is
optional; when omitted, MadFact stores only a generated anonymous participant ID in
`PlayerPrefs`. Display names are not persisted.

## Development configuration

Set the Loki credential in the environment before launching Unity:

```sh
export LOKI_PASSWORD='...'
```

For local Editor development, you can instead fill in the ignored `.env` at the
MadMovieFact project root:

```text
LOKI_USER=beetrap
LOKI_PASSWORD=your_password_here
```

Desktop players that cannot inherit an environment may use:

```text
<Application.persistentDataPath>/madfact.telemetry.local.json
```

with this local-only content:

```json
{"loki_password":"..."}
```

Never place that file under `Assets`, serialize it in a scene, commit it, print it, or
store it in `PlayerPrefs`. Production clients should obtain a short-lived runtime
credential through the deployment platform; a long-lived Loki password embedded in a
client build can always be extracted.

## Event shape

Loki labels are deliberately limited to `app="madfact"` and `source="unity"`.
Participant, session, level, movie, customer, and interaction identifiers remain in the
JSON log body. Gameplay continues if credentials are absent or Loki is unavailable.

The main instrumented actions include session and scene lifecycle, starting/completing
levels, manual/automated/content-based movie recommendations, customer questions,
rule configuration, collaborative-filter vector selection and value changes, optimizer
runs, the privacy scenario, market-gap concepts, and the final greenlight choice.

Use the sibling `MadFactLogger` project to inspect records:

```sh
cd ../MadFactLogger
./start_viewer.sh
```

Then open `http://127.0.0.1:4320`. The viewer and query script read `LOKI_PASSWORD`
independently; the Unity project does not depend on or copy files from that repository.

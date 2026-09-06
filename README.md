# MadFact public website

- Public page: https://inter-play-ai4k12.github.io/MadMovieFact/
- Manuscript companion: https://inter-play-ai4k12.github.io/MadMovieFact/research/
- Browser game: https://inter-play-ai4k12.github.io/MadMovieFact/play/
- Telemetry relay: https://madfact-telemetry.farhadierf.chatgpt.site/api/telemetry

The public website deploys from the existing repository's `gh-pages` branch.
The browser game was rebuilt for this release with optional logging: guests can choose
Play without logging, and profile validation accepts an unchecked consent box.

Run `python3 scripts/prepare-pages.py /path/to/fresh/staging-directory` to combine
this website with `Builds/WebGL`. Only public assets are copied. Unity gzip files
are decompressed for GitHub Pages. Runtime routing in `play/telemetry-config.js`
contains only the public relay URL, never credentials. Bump the build URL cache
version in the packager after future Unity exports.

Publish the staged files using the existing `Builds/Pages` deployment checkout,
preserving `gh-pages` history. Do not force-push or upload environment files.

The separate relay source is in `telemetry-relay`, which has its own hosting
repository and `.openai/hosting.json`. LOKI_USER and LOKI_PASSWORD were configured
as secret runtime values there and as GitHub Actions repository secrets.
GitHub secrets do not automatically sync with the relay; rotate both stores when
changing the Loki password. GitHub Pages does not execute a server-side proxy.

Verification on September 6, 2026: 37 Unity telemetry tests and 7 relay tests passed;
guest play reached Level 1 in the browser. The relay's health endpoint reports
configured credentials, but actual telemetry forwarding returned 502 because the
existing Loki origin returned HTTP 522 on a direct test. Collection requires
restoring the server behind loki-madfact.interplaylab.io. The synthetic diagnostics
use participant ID `synthetic-deployment-check` and contain no participant data.

Live AI poster generation is not configured for this public edition.

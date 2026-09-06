# MadFact public website

Live site: https://inter-play-ai4k12.github.io/MadMovieFact/
Game: https://inter-play-ai4k12.github.io/MadMovieFact/play/

The public website is deployed from the repository's `gh-pages` branch (root folder).
`website/index.html`, `website/style.css`, and `website/assets/` are the editable landing page source.
The deployed game is the existing August 8, 2026 ROC AI 26 WebGL export, not a new Unity build.

Run `python3 scripts/prepare-pages.py /path/to/new/staging-directory` to combine the website
with `Builds/WebGL`. The output directory must not exist. The packager copies only public
assets and decompresses Unity gzip files so GitHub Pages needs no custom encoding headers.
It deliberately excludes local server launchers and environment files.

Publish the resulting directory to `gh-pages`, preserving the existing branch history for
updates. The initial local deployment checkout is `Builds/Pages`; future updates there can
be committed and pushed to `git@github.com:inter-play-AI4K12/MadMovieFact.git`.
Do not force-push or include the main Unity source tree in the deployment branch.

GitHub Pages provides static hosting only. Level 8 live poster generation and the telemetry
relay are unavailable. The existing game requires its study-consent form before gameplay. Cancel returns
to the menu but does not bypass that requirement. The static host has no telemetry relay. A future full-service deployment needs the same-origin server routes
from `scripts/serve_web.py`, server-held credentials, and appropriate usage controls.

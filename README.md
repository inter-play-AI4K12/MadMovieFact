# ROC AI 26

ROC AI 26 is a 2D Unity learning game about recommendation systems. The player inherits
Pellings Video, a struggling 1990s VHS store, and improves its recommendations across five
levels:

**manual recommendations → rule-based recommendations → content-based recommendations →
collaborative filtering → market-gap research → AI-assisted poster making**

The project is developed by the inter.play Lab / AI4K12 project. Original design by Luca
D'Stasio; supervision and development by Erfan Farhadi.

## Requirements

- Unity **6000.5.3f1**
- Universal Render Pipeline **17.5.0**
- Input System **1.19.0**
- Unity UI (uGUI) **2.0.0**
- Unity **WebGL Build Support** module (only required for web exports)

Unity MCP packages are included for editor automation, but the game itself does not require an
active MCP connection to run.

## Run the game

1. Open the project in Unity `6000.5.3f1`.
2. Open `Assets/Scenes/MainMenu.unity`.
3. Press **Play**.
4. Choose **Start Full Game**.

`MainMenu.unity` is the first enabled scene in Build Settings. The full-game button starts a clean
run in `Storefront.unity`, then carries the persistent game state, money, customer history, matrix,
and narrative flags through the dedicated level scenes.

The menu also provides direct access to the storefront and every level for development and testing.

## Create and serve a web build

The reusable exporter builds every enabled scene in Build Settings and creates both a deployable
folder and a ZIP archive. Unity is required only on the computer that creates the build:

```bash
./scripts/build-web.sh
```

Close this project in Unity before running the terminal command. If the project is already open,
use **MadFact → Build → WebGL Export** from Unity's menu instead; both routes use the same exporter.

The outputs are:

- `Builds/WebGL/` — the folder to deploy or run locally.
- `Builds/MadMovieFact-WebGL.zip` — a shareable copy that includes local launchers and the
  telemetry relay.

Build outputs and logs are intentionally ignored by Git. If Unity is installed outside the default
Unity Hub location, provide its executable explicitly:

```bash
UNITY_PATH="/path/to/Unity" ./scripts/build-web.sh
```

Before serving a telemetry-enabled build, create the ignored project-root `.env`:

```text
LOKI_USER=beetrap
LOKI_PASSWORD=your_password_here
OPENAI_API_KEY=your_openai_api_key_here
```

Then serve the latest export:

```bash
./scripts/serve-web.sh
```

The launcher serves Unity's compressed files and exposes same-origin `/api/telemetry` and
`/api/poster/generate` relays. Loki and OpenAI credentials stay in the local server process; they
are never compiled into or returned to the WebGL client. Level 8 uses OpenAI's `gpt-image-2`
model to create portrait poster drafts, with a seven-image cap per game session. Open
<http://localhost:8081/> on the host, or use the host machine's LAN address,
such as `http://192.168.1.20:8081/`, from another device. Port 8081 avoids Unity's temporary
Build & Run preview server, which commonly occupies 8080. Set a different port when needed with
`PORT=9000 ./scripts/serve-web.sh`, and press `Ctrl+C` to stop the server.

On a Windows computer without Unity, extract `MadMovieFact-WebGL.zip`, open the resulting `WebGL`
folder, and double-click `serve-web.cmd`. The launcher starts a local compression-aware server and
opens the game in the default browser. Copy `.env.example` to `.env` beside the launcher and set
`LOKI_PASSWORD` to enable server logging and `OPENAI_API_KEY` to enable Level 8 poster generation.
It uses only Windows PowerShell; Unity, Python, Node.js, and administrator access are not required.

From Command Prompt inside the extracted `WebGL` folder, the same launcher can be started with:

```bat
serve-web.cmd
```

To use a different port in Command Prompt:

```bat
set PORT=9000
serve-web.cmd
```

The launcher handles the local PowerShell execution-policy override. Keep `serve-web.cmd`,
`serve-web.ps1`, `.env`, `index.html`, and the build folders together after extraction. Never
upload or share the populated `.env`. In a development checkout, use `scripts\serve-web.cmd`
from the project root instead.

## Playing an individual level

Every dedicated level scene can be opened and played directly in the Unity editor. When no run is
active, `GameManager.PrepareStandaloneLevel` supplies appropriate starting money, progression, run
state, and matrix values. If the scene was reached from the main menu or storefront, the existing
run is preserved instead.

This makes both workflows valid:

- Start at `MainMenu.unity` to test progression and state carried between scenes.
- Start at any `Level*.unity` scene to work on that mechanic in isolation.

## Scene map

| Scene | Purpose |
| --- | --- |
| `MainMenu.unity` | Authored entry menu with full-game, storefront, and per-level buttons. |
| `Storefront.unity` | VHS shop hub, customer queue, narrative dialogue, and level entrances. |
| `Level01_ManualRecommendation.unity` | Read customer files, ask limited questions, and recommend a tape manually. |
| `Level02_RuleBasedRecommendation.unity` | Program UNIT B-EIGE with rigid `IF genre THEN movie` rules and run customer batches. |
| `Level03_RatingsTable.unity` | Click four customer profiles, read their 0-to-5-star movie-rating rows, and identify row favorites and overall column averages. |
| `Level04_CollaborativeFiltering.unity` | Compare similar customers and predict missing ratings from their shared patterns. |
| `Level05_MatrixFactorization.unity` | Explore latent-vibe sliders, prediction error, and gradient descent. |
| `Level06_ContentBasedRecommendation.unity` | Rank the catalog from customer and movie features, then confront feature-model limits. |
| `Level07_MarketGapResearch.unity` | Use learned rating patterns to assemble and greenlight a movie that fills the market gap. |
| `Level08_PosterGeneration.unity` | Edit the Level 7 brief as a prompt, choose an art style, generate up to seven poster drafts, and compare their history. |
| `MadMovieFact.unity` | Compatibility/all-in-one composition scene; retained for reference, but disabled in Build Settings. |

`Assets/Scripts/Core/LevelSceneCatalog.cs` is the authoritative code-side map for these paths.

## Authored editor structure

The main menu and gameplay screens are visible and editable before entering Play Mode:

- `MainMenu.unity` contains its Canvas, background, window, labels, icons, buttons, and EventSystem.
- Dedicated scenes contain the shared storefront, HUD, dialogue box, bootstrap, and relevant level
  prefab as normal scene objects.
- Level layouts live in `Assets/Prefabs/Levels/`.
- Runtime controllers bind behavior to serialized references instead of replacing the authored
  hierarchy.

Runtime creation is retained for content that is genuinely dynamic, including changing queue
members, selected poster details, ranked suggestions, sale feedback, dialogue choices, and stickers
placed by the player. The poster-browser shell and its initial shelf remain authored in the prefabs.
`RuntimeSkin` reconnects runtime-sliced atlas sprites and OS fonts that Unity cannot serialize
reliably into prefabs.

Useful editor commands are available under the **MadFact** menu:

- **Build → WebGL Export**
- **Rebuild Authored Main Menu**
- **Refresh Editor UI Preview**
- **Refresh Authored Level Prefabs**
- **Validate Authored Level Prefabs**
- **Normalize Dedicated Scene UI Order**
- **Repair Dedicated Level Scene Slots**
- **Bake Serializable Preview Fonts**

## Gameplay arc

### Storefront

The shop is the narrative and progression hub. A growing queue demonstrates why manual service does
not scale, while Mr. Pellings, UNIT B-EIGE, and other characters explain the consequences of each
recommendation approach.

### Level 1 — Manual recommendation

Each customer has a visit-specific history, demand, notes, age, and hidden preferences. The player
spends a limited number of clarifying questions, browses a genre-organized shelf of 17 VHS tapes,
reads age ratings and eight-dimensional box features, and earns or loses money based on the match.

### Level 2 — Rule-based recommendation

The player creates brittle genre-to-movie rules and runs them against a batch of customers. The CRT
log reports perfect sales, close matches, and refunds. A rule that hands an R-rated tape to Timmy
turns the abstract limitation into a concrete oversight lesson and lets the player add an age rule.

### Level 3 — Ratings table

Four known customer profiles reveal rows of zero-to-five-star movie ratings. The player identifies
row favorites, then checks each profile and calculates which movie columns have the highest and
lowest average ratings.

### Level 4 — Collaborative filtering

The mainframe begins with Wendell and Priya in a 2 × 5 ratings matrix. Their first four ratings are
identical, so the player can use Wendell's final rating to predict Priya's one missing value. A larger
5 × 5 matrix then lets the player compare more customers, including a prediction based on the
average of two ratings. Corrections label each customer's original rating and color the player's
guess green, yellow, or red. The lesson ends with a horizontally scrollable 5 × 9 sparse matrix whose
many empty cells motivate the need for a method that can fill incomplete real-world ratings data.

### Level 5 — Matrix factorization

Wendell, Dot, and Hank introduce a small 3 × 3 ratings table summarized by two unnamed hidden
factors. The player adjusts customer and movie profiles by hand until the average gap
between predicted and original ratings falls below 0.35. The lesson then expands to four factors and
a 5 × 5 table. The optimizer is introduced only after the player experiences the extra manual work;
it runs gradient descent to reduce error and expose missing ratings. Its success also triggers the
privacy and popularity-bias scenarios, while money, trust, and narrative flags retain consequences.

### Level 7 — Market-gap creative brief

The player chooses from four categories—settings, characters, story ideas, and props/effects—to
design a family-friendly spooky-comedy concept. Twenty-four options make the design space broader,
while the market-gap meter still asks the player to balance the audience's spooky and funny tastes.
The selected concepts become a categorized creative brief stored in the active run.

### Level 8 — Poster generation

The Level 7 brief becomes editable prompt text. The player can revise it, choose one of six visual
styles, and generate up to seven poster drafts. Previous drafts remain visible as selectable
thumbnails so the player can compare them and choose a final poster. The WebGL client sends the
prompt to the local launcher; only that server reads `OPENAI_API_KEY` and calls the image API.

## State and scenario model

- `CustomerData` contains a customer's persistent identity, age, genre profile, and true taste.
- `MovieData` contains its genre, age rating, visible feature vector, and hidden latent vibe.
- `CustomerVisit` contains one appearance: recent history, stated demand, notes, and return context.
- `LevelScenario` connects a visit to success and failure follow-up dialogue.
- `RunState` remembers visits, recommendations, satisfaction, decisions, and story flags.
- `GameManager` owns persistent money, community trust, phase, unlocks, matrix state, and the active
  run.

This supports returning customers and consequence dialogue without introducing a large quest system.

## Project structure

```text
Assets/
├── Editor/                 MadFact authoring and validation utilities
├── Prefabs/
│   ├── Environment/        Editable storefront environment
│   ├── Levels/             Editable gameplay level layouts
│   └── UI/                 HUD and dialogue presentation
├── Resources/
│   ├── Atlases/            Pixel-art source sheets
│   ├── Audio/Music/        Background music
│   ├── Backgrounds/        Storefront and dedicated level backgrounds
│   └── Characters/         Customer and queue sprites
├── Scenes/                 Main menu, storefront, and eight dedicated levels
└── Scripts/
    ├── Core/               Catalog, customer data, latent vectors, and matrix model
    ├── Levels/             Level mechanics and scene views
    ├── Scenarios/          Customer visits and narrative copy
    ├── Systems/            State, economy, audio, and music
    └── UI/                 Authored UI binding, styling, and sprite slicing
```

## Economy and progression

Recommendation error determines the sale result:

- Perfect match: `+$20`
- Close match: `+$5`
- Poor match/refund: `-$5`

Current progression thresholds are `$100` for completing Level 1 and `$300` for completing Level 2.
Later levels advance through their mechanic-specific completion events rather than additional money
gates.

## Additional documentation

`Assets/Docs/MadFact.md` contains earlier design and implementation notes. Treat the root README and
the current scene/prefab hierarchy as authoritative when those historical notes differ from the
present eight-level build.

## Repository notes

The repository contains only the 2D MadFact game, its Unity configuration, authoring utilities, and
editor integration. Generated `Library/`, `Temp/`, recovery scenes, user settings, and build outputs
remain ignored.

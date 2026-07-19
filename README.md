# MadMovieFact

MadMovieFact is a 2D Unity learning game about recommendation systems. The player inherits
Pellings Video, a struggling 1990s VHS store, and improves its recommendations across five
levels:

**manual recommendations → rule-based recommendations → content-based recommendations →
collaborative filtering → market-gap research and movie making**

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
folder and a ZIP archive:

```bash
./scripts/build-web.sh
```

Close this project in Unity before running the terminal command. If the project is already open,
use **MadFact → Build → WebGL Export** from Unity's menu instead; both routes use the same exporter.

The outputs are:

- `Builds/WebGL/` — the folder to deploy to a static web host.
- `Builds/MadMovieFact-WebGL.zip` — the same export packaged for sharing or uploading.

Build outputs and logs are intentionally ignored by Git. If Unity is installed outside the default
Unity Hub location, provide its executable explicitly:

```bash
UNITY_PATH="/path/to/Unity" ./scripts/build-web.sh
```

To test the latest export locally with Unity's compression-aware web server:

```bash
./scripts/serve-web.sh
```

Then open <http://localhost:8080/>. Set a different port when needed, for example
`PORT=9000 ./scripts/serve-web.sh`, and press `Ctrl+C` to stop the server.

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
| `Level03_ContentBasedRecommendation.unity` | Rank the full catalog from customer and box features, then confront filter bubbles and feature-model limits. |
| `Level04_CollaborativeFiltering.unity` | Explore a Customers × Movies matrix, latent-vibe sliders, prediction error, and gradient descent. |
| `Level05_MarketGapResearch.unity` | Use the learned market gap to assemble and greenlight a new movie poster. |
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

### Level 3 — Content-based recommendation

The TASTE-MATCH 3000 builds genre profiles from rental history, scores the full shelf from the
features printed on each box, and generates ranked suggestions. Returning customers expose a filter
bubble, while a high-scoring but disappointing recommendation demonstrates that a content model can
only reason about the features it was given.

### Level 4 — Collaborative filtering

The mainframe displays known and predicted ratings for a Customers × Movies matrix. Four latent
dimensions—Space-y, Spooky, Funny, and Explosions—can be adjusted manually. The optimizer runs real
gradient descent to reduce total prediction error and expose missing ratings. Its success also
triggers a privacy decision with Gibbs and a popularity-bias complaint from Indie Iris; money, trust,
and narrative flags retain the consequences.

### Level 5 — Market-gap research and movie making

The optimized matrix reveals an underserved Spooky + Funny audience. The player combines weighted
cutouts on a corkboard, matches the target latent profile, and greenlights the resulting movie.

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
├── Scenes/                 Main menu, storefront, and five dedicated levels
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
present five-level build.

## Repository notes

The repository contains only the 2D MadFact game, its Unity configuration, authoring utilities, and
editor integration. Generated `Library/`, `Temp/`, recovery scenes, user settings, and build outputs
remain ignored.

# MadMovieFact

A tactile Unity game that teaches recommendation systems and **Matrix Factorization**
(collaborative filtering — latent features, dot products, loss, and gradient descent) to kids.
You inherit a failing 1990s video store and upgrade it from **manual recommendations → rigid
IF/THEN rules → content-based matching → collaborative filtering**, then use the math to discover
an unserved audience and produce its blockbuster.

*inter.play Lab · AI4K12 — designer: Luca D'Stasio · supervisor: Erfan.*

## Run it
1. Open the project in **Unity 6000.4.7f1** (URP).
2. Open the scene **`Assets/Scenes/MainMenu.unity`**.
3. Press **Play**.

`MainMenu.unity` is first in Build Settings and can launch the full game, storefront, or any
individual level. `MadMovieFact.unity` remains the full playable all-in-one game scene. For focused
editing, open any dedicated scene under `Assets/Scenes/Level*.unity`; each one contains its shared
UI/bootstrap plus the matching level prefab and starts directly at that level with basic standalone
defaults if no existing run is active.

`MadFactBootstrap.StartPhaseOverride` controls whether a scene runs the full intro (`-1`), starts at
the storefront (`0`), or opens directly inside Level 1-5.

## Project structure

- `Assets/Scenes/` — playable scene assets.
- `Assets/Prefabs/Environment/` — editable storefront environment.
- `Assets/Prefabs/UI/` — HUD and dialogue presentation.
- `Assets/Prefabs/Levels/` — one editable prefab per gameplay level.
- `Assets/Scripts/` — behavior, state, binding, and reusable UI helpers.
- `Assets/Scripts/Scenarios/` — authored customer visits, scenario beats, and fixed narrative copy.
- `Assets/Resources/` — generated backgrounds, character art, and source atlases.

## Scene structure

- `Assets/Scenes/MainMenu.unity` — playable entry menu with full-game and per-level launch buttons.
- `Assets/Scenes/MadMovieFact.unity` — full intro-to-finale all-in-one scene.
- `Assets/Scenes/Storefront.unity` — hub / VHS shop floor.
- `Assets/Scenes/Level01_ManualRecommendation.unity` — customer file and counter scene.
- `Assets/Scenes/Level02_RuleBasedRecommendation.unity` — robot/rule terminal scene.
- `Assets/Scenes/Level03_ContentBasedRecommendation.unity` — item-feature matching scene, placeholder for now.
- `Assets/Scenes/Level04_CollaborativeFiltering.unity` — matrix/mainframe scene.
- `Assets/Scenes/Level05_MarketGapResearch.unity` — corkboard/movie-making scene.

`LevelSceneCatalog` is the code-side map for these scene paths and their intended background keys.
The scenes are also listed in Build Settings after `MadMovieFact.unity` so they can be opened or
tested individually.

## Scenario structure

The project now separates persistent customers from per-level visits:

- `CustomerData` describes the person and their true taste.
- `CustomerVisit` describes a specific appearance: recent history, stated demand, file note, and return-visit context.
- `LevelScenario` wraps a visit with success/failure follow-up dialogue.
- `RunState` remembers recommendations, customer satisfaction, visits, and lightweight story flags.

This keeps the game content flexible enough for returning customers and consequence dialogue without
adding a heavy quest system.

## The arc
- **Storefront** — a video store whose customer line grows to show the scaling bottleneck.
- **Level 1 · Manual recommendation** — read customer files, ask limited questions, recommend a tape.
- **Level 2 · Rule-based recommendation** — program a robot with brittle `IF wants=Genre THEN tape` rules.
- **Level 3 · Content-based recommendation** — match explicit movie features to stated customer needs; placeholder mechanic for now.
- **Level 4 · Collaborative filtering** — the CRT Matrix Factorization engine: a Customers×Movies grid with
  live `guess = 1 + 4·dot(U,V)`, draggable latent "vibe" sliders, audible error tension, and an
  **Optimizer** that runs real gradient descent to balance the board and fill in predictions.
- **Level 5 · Market gap research and movie making** — the matrix surfaces an underserved Spooky+Funny audience; design a
  poster on the corkboard to match their latent vibe and greenlight the movie.

See **`Assets/Docs/MadFact.md`** for the full design notes, code map, and tuning knobs.

## Repo notes
The project has been reduced to the MadFact game, its URP configuration, and the core Unity MCP
development bridge. XR, VR-template, tutorial, multiplayer, sample-scene, and package-demo
content has been removed. Unity's generated `Library/` cache remains ignored.

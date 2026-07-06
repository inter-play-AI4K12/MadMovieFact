# MadMovieFact

A tactile Unity game that teaches **Matrix Factorization** (collaborative filtering — latent
features, dot products, loss, and gradient descent) to kids. You inherit a failing 1990s video
store and upgrade it from **manual recommendations → rigid IF/THEN rules → a learning
algorithm**, then use the math to discover an unserved audience and produce its blockbuster.

*inter.play Lab · AI4K12 — designer: Luca D'Stasio · supervisor: Erfan.*

## Run it
1. Open the project in **Unity 6000.4.7f1** (URP).
2. Open the scene **`Assets/Scenes/MadMovieFact.unity`**.
3. Press **Play**.

The gameplay scene is authored from reusable prefabs. Open `MadFactCanvas` in the Hierarchy to
edit the storefront, HUD, dialogue box, and each level directly. `MadFactBootstrap` coordinates
game flow and retains a runtime-construction fallback, but the normal scene path uses serialized
prefab references.

## Project structure

- `Assets/Scenes/` — playable scene assets.
- `Assets/Prefabs/Environment/` — editable storefront environment.
- `Assets/Prefabs/UI/` — HUD and dialogue presentation.
- `Assets/Prefabs/Levels/` — one editable prefab per gameplay level.
- `Assets/Scripts/` — behavior, state, binding, and reusable UI helpers.
- `Assets/Resources/` — generated backgrounds, character art, and source atlases.

## The arc
- **Storefront** — a video store whose customer line grows to show the scaling bottleneck.
- **Level 1 · Manual** — read customer files, ask limited questions, recommend a tape.
- **Level 2 · Automation** — program a robot with brittle `IF wants=Genre THEN tape` rules.
- **Level 3 · Algorithm** — the CRT Matrix Factorization engine: a Customers×Movies grid with
  live `guess = 1 + 4·dot(U,V)`, draggable latent "vibe" sliders, audible error tension, and an
  **Optimizer** that runs real gradient descent to balance the board and fill in predictions.
- **Level 4 · Market Gap** — the matrix surfaces an underserved Spooky+Funny audience; design a
  poster on the corkboard to match their latent vibe and greenlight the movie.

See **`Assets/Docs/MadFact.md`** for the full design notes, code map, and tuning knobs.

## Repo notes
The project has been reduced to the MadFact game, its URP configuration, and the core Unity MCP
development bridge. XR, VR-template, tutorial, multiplayer, sample-scene, and package-demo
content has been removed. Unity's generated `Library/` cache remains ignored.

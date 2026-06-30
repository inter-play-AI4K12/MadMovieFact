# MadMovieFact

A tactile Unity game that teaches **Matrix Factorization** (collaborative filtering — latent
features, dot products, loss, and gradient descent) to kids. You inherit a failing 1990s video
store and upgrade it from **manual recommendations → rigid IF/THEN rules → a learning
algorithm**, then use the math to discover an unserved audience and produce its blockbuster.

*inter.play Lab · AI4K12 — designer: Luca D'Stasio · supervisor: Erfan.*

## Run it
1. Open the project in **Unity 6000.3.8f1** (URP).
2. Open the scene **`Assets/MadFact/MadFact.unity`**.
3. Press **Play**.

The entire game builds itself at runtime from the single `MadFact` GameObject
(`MadFactBootstrap`). All UI, sprites, fonts, and audio are generated procedurally in code —
there are **no binary art or sound assets**.

## The arc
- **Storefront** — a video store whose customer line grows to show the scaling bottleneck.
- **Level 1 · Manual** — read customer files, ask limited questions, recommend a tape.
- **Level 2 · Automation** — program a robot with brittle `IF wants=Genre THEN tape` rules.
- **Level 3 · Algorithm** — the CRT Matrix Factorization engine: a Customers×Movies grid with
  live `guess = 1 + 4·dot(U,V)`, draggable latent "vibe" sliders, audible error tension, and an
  **Optimizer** that runs real gradient descent to balance the board and fill in predictions.
- **Level 4 · Market Gap** — the matrix surfaces an underserved Spooky+Funny audience; design a
  poster on the corkboard to match their latent vibe and greenlight the movie.

See **`Assets/MadFact/README.md`** for the full design notes, code map, and tuning knobs.

## Repo notes
This repository excludes large unrelated sample asset packs that shipped in the original Unity
template (Furniture Mega Pack, SkySeries, VR template) and the usual Unity `Library/` cache —
none are required to build or play MadFact.

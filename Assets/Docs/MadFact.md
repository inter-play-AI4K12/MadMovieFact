# MadFact — a tactile game about Matrix Factorization

A playable prototype that turns collaborative filtering (latent features, dot products,
loss, and gradient descent) into a hands-on puzzle. You inherit a failing 1990s video
store and upgrade it from **manual recommendations → rigid rules → a learning algorithm**,
then use the math to discover an unserved market and produce its blockbuster.

## How to run
1. Open the project in Unity **6000.4.7f1** (or compatible).
2. Open the scene **`Assets/Scenes/MadMovieFact.unity`**.
3. Press **Play**. The whole game builds itself at runtime from the single `MadFact`
   GameObject (which carries the `MadFactBootstrap` component).

> UI, fonts, and audio are generated procedurally in code. Imported character art lives under
> `Resources/Characters/` and is loaded by the runtime-built UI.

## The arc (7 playable levels + hub)
- **Storefront (hub):** a top-down store whose customer line grows to show the scaling
  bottleneck. The empty **HORROR / COMEDY** shelves quietly foreshadow the market gap.
- **Level 1 — Manual Era (`Level1Counter`):** read a manila customer file, ask a *limited*
  number of clarifying questions (action economy), recommend a tape. Pay scales with match.
- **Level 2 — Automation Era (`Level2Robot`):** program the beige robot with rigid
  `IF wants=Genre THEN recommend Tape` rules on a DOS terminal. Brittle rules fail on
  contradictory customers and tank revenue.
- **Level 3 — Ratings Table (`Level3RatingsTable`):** four customer profiles expose rows
  of 0-to-5-star ratings. Players find each person's favorite and least favorite movie,
  then compare movie-column averages across all four people.
- **Level 4 — Collaborative Filtering (`Level3Mainframe`):** begin with two established
  customers whose 2×5 rating rows nearly match, then solve a 5×5 comparison task (including
  an average-of-two-customers clue). A final scrollable 5×9 sparse table shows that most
  people have not rated most movies and motivates learning how to fill missing values.
- **Level 5 — Matrix Factorization (`Level3Mainframe`):** the CRT factorization engine.
  A 3×3 tutorial reuses Wendell, Dot, and Hank and summarizes their ratings with two
  unnamed hidden factors. Players manually adjust customer/movie
  profiles until mean prediction error falls below 0.35. The lesson then expands to the
  full 5×5 table and four factors; only there is the **OPTIMIZER** introduced to run real
  gradient descent, fill empty cells, and reveal an underserved cluster.
- **Level 6 — Content-Based (`Level6ContentBased`):** compare customer preferences with
  movie features and confront the limits of box-label data.
- **Level 7 — The Market Gap (`Level7Corkboard`):** the matrix surfaced a Spooky+Funny
  crowd no tape serves. Drag magazine-cutout stickers (each carrying latent weights) onto a
  poster to match that gap's vibe, then **GREENLIGHT** the blockbuster.

## The "Tension" mechanic (`AudioTension`)
All audio is synthesized at runtime:
- Mathematical error → low-pass **brown-noise** hum that swells with the error; high error
  adds pitch **wow-and-flutter** (a motor struggling).
- Perfect match → silence punctuated by a satisfying **VHS clunk**.
- Economy: **cash register** (perfect, +$20), **coin** (close, +$5), **buzzer** (refund, −$5).

## Economy (revenue = loss function)
`Economy.cs`: error `< 0.5` → +$20 (Perfect), `< 2.0` → +$5 (Close), else −$5 (Refund).
Level caps are monetary (`$40` to automate, `$110` to boot the mainframe).

## Code map (`Assets/Scripts`)
- `Core/` — `Latent` (the 4 vibes + dot product), `GameData` (catalog + customers),
  `MfModel` (the live matrix + gradient descent), `MfMath`.
- `Systems/` — `GameManager` (state/money), `Economy`, `AudioTension` (procedural sound).
- `UI/` — `Theme` (Corporate Lo-Fi palette, bevel sprites, CRT overlays, OS fonts),
  `UIFactory` (panels/buttons/sliders), `CommsBox` (Old Dude FMV vs. Robot icon), `Hud`.
- `Levels/` — `StorefrontView`, `Level1Counter`, `Level2Robot`, `Level3RatingsTable`,
  `Level3Mainframe`, `Level6ContentBased`, `Level7Corkboard`.
- `Resources/Characters/` — imported, runtime-loaded character sprites grouped by role.
- `Resources/Atlases/` — original supplied pixel-art sheets; `UI/ArtSprites.cs` slices and
  caches portraits, covers, stickers, HUD graphics, and action icons at runtime.
- `MadFactBootstrap.cs` — builds the camera/canvas/EventSystem and drives the narrative flow.

## Notes & honest deviations from the GDD
- **Most art remains stylized procedural placeholder work** (dithered FMV portrait, 1-bit
  robot, procedural bevels). The storefront queue now includes one imported movie-fan sprite,
  establishing the path for gradually replacing the remaining placeholders.
- **CRT look** is done with UI overlays (scanlines + vignette + phosphor tint) rather than
  URP post-processing volumes (bloom/chromatic aberration), to keep the build dependency-free
  and robust. URP is installed, so a post-processing pass can be added as polish.
- **Pixel Perfect Camera** is not attached: the game is Screen-Space-Overlay UI, where it has
  little effect. Easy to add for any world-space sprite work.
- **Corkboard is an overlay**, not a separate loaded scene. It's a self-contained controller,
  so splitting it into its own scene later is trivial.
- The optimizer's learning rate (`0.004`) was tuned empirically: it drives mean error from
  ~0.9 to <0.05 in ~110–180 steps. (An earlier `0.02` diverged.)

## Quick designer knobs
- Catalog & customers: `Core/GameData.cs` (latent vibes, the underserved demographic).
- Payment bands: `Systems/Economy.cs`. Level goals: `Systems/GameManager.cs`.
- Palette & fonts: `UI/Theme.cs`. Optimizer lr/steps: `Levels/Level3Mainframe.cs`.

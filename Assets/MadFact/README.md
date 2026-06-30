# MadFact — a tactile game about Matrix Factorization

A playable prototype that turns collaborative filtering (latent features, dot products,
loss, and gradient descent) into a hands-on puzzle. You inherit a failing 1990s video
store and upgrade it from **manual recommendations → rigid rules → a learning algorithm**,
then use the math to discover an unserved market and produce its blockbuster.

## How to run
1. Open the project in Unity **6000.3.8f1** (or compatible).
2. Open the scene **`Assets/MadFact/MadFact.unity`**.
3. Press **Play**. The whole game builds itself at runtime from the single `MadFact`
   GameObject (which carries the `MadFactBootstrap` component).

> Everything — UI, sprites, fonts, and audio — is generated procedurally in code.
> There are **no binary art or sound assets**, so the game is fully self-contained.

## The arc (4 levels + hub)
- **Storefront (hub):** a top-down store whose customer line grows to show the scaling
  bottleneck. The empty **HORROR / COMEDY** shelves quietly foreshadow the market gap.
- **Level 1 — Manual Era (`Level1Counter`):** read a manila customer file, ask a *limited*
  number of clarifying questions (action economy), recommend a tape. Pay scales with match.
- **Level 2 — Automation Era (`Level2Robot`):** program the beige robot with rigid
  `IF wants=Genre THEN recommend Tape` rules on a DOS terminal. Brittle rules fail on
  contradictory customers and tank revenue.
- **Level 3 — Algorithm Era (`Level3Mainframe`):** the CRT Matrix Factorization engine.
  A Customers×Movies grid shows a **Target** and a live **Guess = 1 + 4·dot(U,V)**.
  Click a row/column to open four plastic **vibe sliders** (Space-y, Spooky, Funny,
  Explosions). Tuning one cell breaks others (red glow + audio static — the coupling you
  *feel*). The **OPTIMIZER** runs real gradient descent to balance the whole board, fills
  empty cells with predictions, and reveals an underserved cluster.
- **Level 4 — The Market Gap (`Level4Corkboard`):** the matrix surfaced a Spooky+Funny
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

## Code map (`Assets/MadFact/Scripts`)
- `Core/` — `Latent` (the 4 vibes + dot product), `GameData` (catalog + customers),
  `MfModel` (the live matrix + gradient descent), `MfMath`.
- `Systems/` — `GameManager` (state/money), `Economy`, `AudioTension` (procedural sound).
- `UI/` — `Theme` (Corporate Lo-Fi palette, bevel sprites, CRT overlays, OS fonts),
  `UIFactory` (panels/buttons/sliders), `CommsBox` (Old Dude FMV vs. Robot icon), `Hud`.
- `Levels/` — `StorefrontView`, `Level1Counter`, `Level2Robot`, `Level3Mainframe`,
  `Level4Corkboard`.
- `MadFactBootstrap.cs` — builds the camera/canvas/EventSystem and drives the narrative flow.

## Notes & honest deviations from the GDD
- **Art/photos/fonts are stylized placeholders generated in code** (dithered FMV portrait,
  1-bit robot, procedural bevels). They establish the *Corporate Lo-Fi* look but are not
  final hand-drawn pixel art. Real sprite/FMV assets can be dropped in later.
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

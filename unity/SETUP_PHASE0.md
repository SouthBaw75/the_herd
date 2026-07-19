# Phase 0 setup — Unity 6 LTS (URP 2D)

A step-by-step for the product owner to stand up the Phase 0 scene and run the
**exit test: click a placeholder animal and advance time to see the date tick.**

> Authored without a Unity editor (headless container). Every step is best-effort
> against Unity 6 (6000.x) + URP; menu paths and versions marked `VERIFY` need a
> quick confirmation on your machine. Nothing here has been compiled or run.

---

## 0. Prerequisites

- Unity Hub + a **Unity 6 LTS (6000.x)** editor installed (D2).
- .NET SDK 8 (already used by `/sim`) to build the sim DLL.
- Git + Git LFS initialized (design doc §6 — LFS for art later).

## 1. Create / open the project

1. This repo already contains `unity/Assets`, `unity/Packages`,
   `unity/ProjectSettings`. In Unity Hub choose **Open > Add project from disk**
   and point at the `unity/` folder. Unity will generate `Library/` (ignored) and
   the binary `ProjectSettings/*.asset` files on first open.
2. Let Package Manager resolve the manifest (URP, 2D feature set, Input System,
   Test Framework, Newtonsoft, uGUI/TMP). If a version string fails to resolve,
   see "Package version notes" at the bottom.
3. When prompted to import **TMP Essentials**, accept it (needed for
   `GameClockView`).

## 2. Apply project settings

Follow `ProjectSettings/README_SETTINGS.md` end to end. The load-bearing ones for
Phase 0:
- URP 2D render pipeline asset assigned (Graphics + Quality).
- Color space = Linear.
- **Api Compatibility Level = .NET Standard 2.1** (so the sim DLL references).
- **Active Input Handling = Both** (so the click + keypress work).

## 3. Reference the sim

Follow `Assets/CattleRanch/README_ASMDEF.md`: build `CattleRanch.Sim.dll` and copy
it to `Assets/CattleRanch/Plugins/`. Confirm the editor shows no missing-reference
errors on `CattleRanch.Game`.

> Until the sim actually ships source + a factory API, the DLL exports only the
> types described in `docs/ARCHITECTURE.md`. The stubs are written to degrade
> gracefully (they log a warning and no-op) if no state is bound yet, so you can
> still complete the scene wiring and the *time-ticks* half of the exit test once
> `GameClock` exists. See "Known gap" at the end.

## 4. Build the isometric tilemap test scene

1. `File > New Scene > Basic (URP)` `VERIFY` template name. Save as
   `Assets/CattleRanch/Scenes/Phase0.unity`.
2. Camera: select **Main Camera**, set **Projection = Orthographic** (2D). Add an
   **Audio Listener** if not present. Keep it as the URP camera.
3. Create the isometric grid: `GameObject > 2D Object > Tilemap > Isometric`
   (`VERIFY` — Unity 6 also has "Isometric Z as Y"; pick **Isometric Z as Y** if
   your art will stack height, otherwise plain **Isometric** is fine for Phase 0).
   This creates a `Grid` with **Cell Layout = Isometric** and a child `Tilemap`.
4. Make a couple of placeholder iso tiles:
   - Import or draw a simple diamond sprite (a 256×128-ish diamond). Set its
     **Sprite Mode**, and **Pixels Per Unit** to match your cell size. `VERIFY`
     PPU vs. Grid `Cell Size` so tiles line up (classic first-hour mismatch).
   - Set the sprite's **Pivot = Bottom** (or a custom pivot at the diamond's
     bottom point) — this matters for sorting (step 6).
   - `Window > 2D > Tile Palette`, create a palette, drag the sprite in, and
     paint a small patch of ground on the Tilemap.

## 5. Drop a placeholder "cow"

1. `GameObject > 2D Object > Sprite` (or a 3D Cube if you prefer the doc's
   "cube/placeholder"). Name it `Cow_Placeholder`. Position it on the tilemap.
2. Give it a collider so it is clickable:
   - Sprite → add **Box Collider 2D** (or **Circle Collider 2D**).
   - Cube → it already has a Box Collider (3D). For 3D colliders with a 2D/ortho
     camera, add a **Physics Raycaster** to the camera. `VERIFY` — with a 2D
     collider + `OnMouseDown`, a Physics2DRaycaster is generally not required, but
     confirm clicks register.
3. Add the **`PlaceholderAnimalView`** component to `Cow_Placeholder`.

## 6. Fix isometric sprite sorting (design doc §6 — "the classic time sink")

The pitfall: in an isometric view, whichever sprite is drawn **in front** must be
the one **lower on the screen** (nearer the camera), regardless of draw order.
Out of the box, sprites at the same Z sort by draw order / distance, so a cow
behind a fence renders on top of it — the depth looks wrong.

**The concrete fix — sort along a custom axis by Y:**

1. `Edit > Project Settings > Graphics` → **Camera Settings** (`VERIFY`: in
   URP 6000.x this may live under the **Renderer2D** asset or the **Camera**
   component instead of Graphics — check all three; the *setting names* are the
   same):
   - **Transparency Sort Mode = Custom Axis**
   - **Transparency Sort Axis = (0, 1, -0.26)**
     - `X=0, Y=1` → sprites sort primarily by world Y (lower on screen = drawn
       in front). The small `Z=-0.26` biases stacked/height sprites correctly for
       an iso projection. `Y=1, Z=0` is the simpler flat-iso variant if you did
       **not** choose "Isometric Z as Y".
2. On every sprite renderer that must sort this way (the cow, any tile props),
   set **Sprite Sort Point = Pivot**, and make sure each sprite's **pivot is at
   its base/feet**. Sorting uses the pivot, so a center pivot makes tall sprites
   sort from their middle and clip incorrectly.
3. For the Tilemap: on the `Tilemap Renderer`, set **Mode = Individual** if tiles
   need to interleave with the cow by Y (Chunk mode batches a whole tilemap into
   one sort key and will not interleave with sprites). `VERIFY` — Individual is
   heavier; for a flat ground layer under the cow, Chunk is fine and Individual is
   only needed for props that must sort against the animal.
4. If you used **Isometric Z as Y**, also confirm the Grid's setting so painted
   tiles get a Z derived from Y for automatic depth.

Result to look for: move the cow up/down the diamond grid in Play mode and it
should pass **behind** tiles/props that are lower on screen and **in front** of
ones higher up.

## 7. Wire the runner + views

1. Create an empty GameObject `GameController`. Add **`RanchRunner`**. Set its
   **Seed** and confirm **Advance Day Key = Space**.
2. Build a UI date label:
   - `GameObject > UI > Text - TextMeshPro` (creates a Canvas + TMP label).
   - On the Canvas or label object, add **`GameClockView`**. Assign:
     - **Runner** = the `GameController` (RanchRunner).
     - **Label** = the TMP text object.
3. (Optional) Add a UI Button "Advance Day" and hook its **OnClick** to
   `RanchRunner.AdvanceDay()` — a mouse alternative to the Space key.
4. When the sim exposes a real spawn/bootstrap API, have your bootstrap script
   call `PlaceholderAnimalView.Bind(animal)` and `RanchRunner.Bind(state, clock)`
   so the placeholder points at a real `Animal` and the label shows a real date.

## 8. Phase 0 exit test

1. Press **Play**.
2. **Click the cow** → Console logs a `[PlaceholderAnimalView] Selected animal…`
   line (or the "no Animal bound yet" placeholder line until the sim spawns a
   herd). Clicking registering at all = the collider/sorting/camera picking works.
3. **Advance time** → press **Space** (or the Advance Day button). The
   `GameClockView` label should tick: `Year 1 — Spring — Day 1` → `Day 2` → …,
   rolling into `Summer` after 28 days (D6). Ticks are day-atomic and driven only
   by your input — never by frame rate.

Passing both = Phase 0 exit test met (design doc §7).

---

## Known gap (must flag to the lead)

At authoring time the sim source was **mid-build by a parallel track**:
`GameClock`, `GameDate`, `Season`, `Animal`, `Genome`, `Paddock`, `IRandom` etc.
**exist and were verified** against these stubs — but `RanchState` (the
serializable root) **did not exist yet**. Consequences:

- `RanchRunner` constructs its own `GameClock` in `Awake()`, so the **date-ticks**
  half of the exit test works as soon as the sim DLL builds — no `RanchState`
  needed. `RanchState` is still referenced (per the task spec) and will fail to
  compile until that type lands; if you build the DLL before `RanchState` exists,
  temporarily comment the `RanchState _state` field/usages, or wait for it.
- The **click** half works immediately (placeholder log), and shows a real
  animal once a spawner calls `PlaceholderAnimalView.Bind(animal)`.

This is expected sequencing (D1: sim-core-first), not a bug in the stubs. Because
the sim was under active parallel development, **re-verify the sim API** (esp.
any `RanchState` factory) before relying on the wiring.

## Package version notes (all `VERIFY` in editor)

These are plausible Unity 6000.0 LTS versions, guessed without a live Package
Manager. If any fails to resolve, delete just the version string in
`Packages/manifest.json` and let Package Manager pick, or add the package by name
in the editor:

| Package | Pinned | Note |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.0.3 | URP 17 = Unity 6 line |
| `com.unity.feature.2d` | 2.0.1 | 2D feature set (sprite, tilemap editor, iso brushes, animation, pixel-perfect) |
| `com.unity.inputsystem` | 1.11.2 | new Input System (installed; stubs use legacy Input via "Both") |
| `com.unity.test-framework` | 1.4.6 | required package in the task |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 | D5 save/load |
| `com.unity.ugui` | 2.0.0 | uGUI + TextMeshPro merged in Unity 6 |

Built-in `com.unity.modules.*` are pinned at `1.0.0` (the standard for modules);
Unity will reconcile these on open.

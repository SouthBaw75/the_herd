# AI Sprite Studio — Research & Build Spec

Research into **Makko.ai** and a concrete plan for a **standalone program** that
does what we actually want from it: *give it a reference image, get back
game-ready animations.*

Researched 2026-07-25. Nothing here is committed to as a decision yet — this is
the evidence and the recommended shape. Decisions go in `DECISIONS.md`.

---

## 0. Research constraints (read this before trusting a number)

This container's egress policy blocked **every** web host at the proxy
(`403 CONNECT`), including `makko.ai`, `blog.makko.ai`, and third-party review
sites. Only `raw.githubusercontent.com` / `api.github.com` are reachable, and
GitHub reads are scoped to this repo only.

So findings came from **web search result summaries**, not from reading Makko's
pages, signing up, or inspecting their network traffic. Consequence:

| Confidence | What |
|---|---|
| **High** | Feature set, asset types, Collections/reference-image model, credit costs, pricing tiers, export format (PNG), the fact that anchor points + a manifest are user-facing concepts |
| **Medium** | The pipeline *stage order* (§2) — inferred from their own blog language plus the 9× credit premium on animation |
| **Low / unverified** | Which models they call, exact frame counts, exact canvas sizes, sheet layout, whether animation is I2V or grid-image |

**To close the gap:** sign up for the free tier (150 art credits/month), run one
character through it, and keep the outputs. One real sprite sheet + its manifest
answers every Low-confidence row above. That is the single highest-value next
step and it costs $0.

---

## 1. What Makko.ai actually is

An AI-powered **2D game studio** in the browser (launched on Product Hunt
2026-04-20, >40,000 assets generated in beta). Two halves:

1. **Art Studio** — generates concept art, characters, props, backgrounds, and
   **animated sprite sheets** in a consistent style.
2. **Game builder** — natural-language → playable browser game (mechanics, level
   layout, controls), published to a public URL. 50+ templates.

**Only half #1 is our target.** The game-builder half is irrelevant to us — we
already have a Unity project and a real simulation core.

### 1.1 Collections — the consistency mechanism

This is the load-bearing idea, and the part most naive clones get wrong.

- A **Collection** is a per-project container for the game's whole visual world.
- You seed it with **concept art** (generated from a description) and/or
  **uploaded reference images** — sketches, photos, existing art.
- **Up to 16 reference images per Collection**; **up to 3 selected per
  generation** to steer that specific output.
- Every later generation in the Collection inherits that style. Characters,
  props, and backgrounds come out looking like one game rather than one
  afternoon of prompting.

Takeaway: style consistency is **state that lives in the project**, not a magic
prompt. Our tool needs the same primitive — a persisted style anchor set that
every generation call automatically conditions on.

### 1.2 The animation feature (the thing we want)

- Generates complete sprite sheets for **walk, run, attack, idle, and custom
  states**.
- Output: **transparent-background PNGs**, animation-ready frame extraction,
  sprite sheet export, full commercial rights.
- **Frame count is a deliberate authoring control** — their own guidance is to
  generate a sequence, identify the **key poses**, and **delete filler frames**,
  because a leaner sheet reads snappier. Frame count drives "emotional pacing."
- An **Alignment Editor** exposes **scale**, **anchor points**, and **manifest**
  config, with a distinction between *manifest-specific* and *global* edits.
  Their stated failure mode is characters "floating or sinking" in-engine
  because the engine doesn't know the anchor point or scale.

That last bullet is the most important finding in this document. **The polished
part of Makko is not the diffusion model — it is the boring deterministic
plumbing around it:** alpha hygiene, anchor points, scale normalization, and a
machine-readable manifest. That plumbing is fully reproducible by us. The
generative step is a paid API call anyone can make.

### 1.3 Credits & pricing (High confidence)

| Item | Credits |
|---|---|
| Character concept / prop / background image | **5** |
| Character reference sheet | **12** |
| **Sprite animation** | **from 45** |

| Plan | Price | Art budget |
|---|---|---|
| Free | $0 | 150 art credits/mo (+70 weekly AI coding requests) |
| Starter | $20/mo | 1,000 art credits/mo, 500 chat credits/wk |
| Creator | $50/mo | 3,000 art credits/mo, 1,500 chat credits/wk |
| Hobbyist / Pro | — | ~6,000 / ~13,000 credits |

Top-ups never expire. Their own worked example: 10 characters × 4 animations +
20 backgrounds ≈ **1,950 credits**.

**The economics that justify building this.** On Starter, 1,000 credits ÷ 45 =
~22 animations/month for $20 → **≈$0.90 per animation**. §5 shows the same
output costs us **≈$0.05–0.50 per animation** in raw API spend, with no monthly
floor and no cap. For a project that needs cattle × 8 directions × 5 states,
that difference is the whole argument.

Note the credit ladder: 5 → 12 → 45. Animation is **9× a still image**. That is
too big a jump for "a few more image calls" and is consistent with either a
video-model call or a multi-call sequence with retries. Useful signal for §3.

### 1.4 Honest limitations (from third-party reviews)

Rated ~7/10 and ~8.5/10. Reviewers' consensus: *"Makko is not a Unity killer.
It's a pre-Unity tool"* — for validating an idea at $0, then moving to Unity.
Browser-only, desktop/laptop. Top complaint: can't export a shippable build.

For us that critique is irrelevant — **we only want the asset pipeline**, and we
want it as a local program we can rerun 200 times without a credit meter.

---

## 2. The reverse-engineered pipeline

Assembled from Makko's own blog vocabulary ("state rows", "asset hygiene",
"frame extraction", "alignment", "manifest", "hitboxes") plus how the credible
open-source equivalents are built. **Seven stages.** The AI touches two of them.

```
[1] STYLE ANCHOR      reference image(s) → persisted Collection (≤16 refs, ≤3 per call)
[2] IDENTITY LOCK     ref → character reference sheet / turnaround   ← AI  (12 cr)
[3] MOTION            ref sheet + state → raw frames                ← AI  (45 cr)
[4] ALPHA             chroma bg → true alpha, soft-alpha unmix, de-fringe
[5] NORMALIZE         trim → common canvas → anchor/pivot → palette lock → deflicker
[6] CULL & PACK       key-pose selection → atlas layout → manifest.json
[7] ENGINE            Unity/Godot importer: slice, pivot, PPU, AnimationClips
```

Stage 2 is why animation is expensive and why quality holds: you do **not**
animate straight from a user photo. You first spend one call converting the
reference into a **canonical, style-locked character sheet** (clean pose, known
scale, known silhouette, plain background). Everything downstream conditions on
*that*, not on the messy input. This is the single biggest quality lever in the
whole system and matches the 5 → 12 → 45 credit ladder exactly.

Stages 4–6 are deterministic image processing. **No model, no randomness, no API
cost.** This is where "looks like AI slop" becomes "drops into Unity and just
works," and it's where our effort should concentrate.

---

## 3. Stage 3 — three ways to generate motion

The core technical decision. All three are viable in July 2026.

### Strategy A — Grid / state-row image generation *(recommended primary)*

One image call renders a whole animation state as a row (or 3×3 grid) of poses
on a flat chroma background. Frames are then cut on a known lattice.

- **Pro:** cheapest (1 call per state), deterministic layout, no temporal
  drift, ideal for pixel art, trivially parallel across states.
- **Con:** limited frame count per call; the model must be good at grid layout;
  inter-frame spacing can be uneven.
- **Proven by:** PixelLab's 3×3 8-direction tool, and `aldegad/sprite-gen`, whose
  description matches this pipeline almost stage-for-stage — *state rows, locked
  character identity, chroma→real alpha, per-pose transparent frame extraction,
  runtime atlas with `manifest.json.frame_layout` carrying absolute frame rects
  and per-state fps/loop flags, plus per-state GIF + contact-sheet QA.* Treat it
  as the reference architecture. (I could not read its source — GitHub scope.)

### Strategy B — Image-to-video, then extract frames

Feed the canonical sheet to an I2V model, get a clip, extract frames, cull to
key poses.

- **Pro:** genuinely smooth, physically plausible motion; great for
  high-res/non-pixel styles.
- **Con:** ~10× the cost; **video models do not emit alpha** — you must prompt a
  chroma background and key it; **palette drifts frame to frame** (documented
  RIFE/pixel-art problem: no "limit to original palette" and alpha often lost to
  a white background). Needs the heaviest §5 post-processing.
- **This is probably what Makko does for the 45-credit tier** — their "generate a
  sequence, then delete filler frames" guidance is exactly a video-extraction
  workflow. Medium confidence.
- Models (July 2026): **Seedance 2.0** (#1 on Artificial Analysis I2V blind
  votes), **Kling 3.0** (~$0.084–0.168/s), **Veo 3.1** (best all-round, 4K),
  **Wan 2.5/2.6** ($0.05/s, or local). Note: **Sora 2's API shuts off
  2026-09-24 — do not build on it.**

### Strategy C — Pose-conditioned per-frame generation *(local, most control)*

Drive each frame with an explicit skeleton, conditioned on the reference for
identity. This is the academic solution: **Sprite Sheet Diffusion**
(arXiv 2412.03685, Hsieh/Zhang/Yan) = **ReferenceNet** (spatial-attention
appearance preservation) + a lightweight **Pose Guider** (pose signal into
denoising) + a **Motion Module** (temporal stability). In practice: ControlNet
OpenPose for spatial control + IP-Adapter for identity, or **Wan 2.2 VACE**
pose-driven workflows in ComfyUI (DWPreprocessor for pose, SAM2/SAM3 +
BiRefNet for masks).

- **Pro:** exact frame count, exact pose per frame, **reusable skeleton library**
  → every character animates identically. Zero marginal API cost once local.
- **Con:** needs a real GPU; heaviest to build; ComfyUI as a dependency.
- **Big win for us:** a hand-authored 8-direction cattle skeleton set, reused
  across every animal and every state, is exactly the determinism this repo
  already believes in.

### Recommendation

Build the tool **provider-agnostic** and ship **A first**, because it's the
cheapest path to a real sprite sheet in Unity. Add **B** behind the same
interface for smooth/high-res work. Treat **C** as the later "studio mode" once
the deterministic stages 4–6 are solid — those stages are shared by all three,
so they're never wasted work.

### Stage 2 — identity lock, model options (July 2026)

| Model | Why |
|---|---|
| **Nano Banana Pro / 2** (Gemini 3 Pro Image) | Best-in-class character consistency across a series; multi-image fusion up to 20 refs; conversational editing. **$0.134/img** (1K–2K), $0.24 (4K); **Nano Banana 2 = $0.067/img**, 3× cheaper |
| **FLUX.2 [dev]** | Combines up to **10 reference images**, keeps character + style consistent, **runs locally** |
| **Qwen-Image-Edit-2511** | Surgical edits (swap gear, fix a detail) without breaking the rest; strong consistency + multi-image fusion |
| **Retro Diffusion** | Purpose-trained *true* pixel art (artist-built, licensed training data). Available via Scenario's API. Use when the target is real pixel art rather than a downscale |

Good split: **Nano Banana 2** for identity lock and turnarounds, **Qwen-Image-Edit**
for targeted revisions ("same cow, add ear tag"), **Retro Diffusion** if we go
true pixel art.

---

## 4. Competitive landscape (steal the good parts)

| Tool | Relevant strength |
|---|---|
| **PixelLab** | Deepest dedicated pixel-art tool. Skeleton-based rigging, text-prompted animation, **one-click 4/8-direction rotations with isometric support**, true inpainting, tilesets. The closest thing to what we want |
| **Retro Diffusion** | True pixel art model; weaker sprite/animation tooling |
| **Scenario** | Style *training* (train a model on our art). Images only — no sprite-sheet or animation pipeline |
| **Spriteflow / Ludo.ai / AutoSprite / spritesheets.ai** | 8-direction turnarounds; Ludo does 8 angles × 3 elevations (eye-level/elevated/high) — directly relevant to an isometric camera |
| **Sorceress "3D→2D"** | Renders 3D models to sprite sheets. **The non-AI escape hatch** — perfectly consistent, free per-frame, arbitrary angles |
| **`aldegad/sprite-gen`** | OSS reference architecture for stages 3–6 (§3A) |
| **Sprite Sheet Diffusion** | The academic method for stage 3C |

**Note the 3D→2D option seriously.** A cheap 3D cow, rigged once, rendered from 8
isometric angles gives *perfect* multi-directional consistency — the exact thing
diffusion is worst at — at zero marginal cost, forever. Worth prototyping
alongside the AI path before committing.

---

## 5. Stages 4–6 — the deterministic core (where we win)

This is our actual product. Non-deterministic model output goes in; a consistent
quality bar comes out. Same seed + same input ⇒ same sheet, cached by content
hash. It fits this repo's determinism ethos exactly.

**[4] Alpha.**
- Generate on a **flat chroma background** (magenta `#FF00FF` — never occurs in
  natural art) and key it. More reliable than asking a model for transparency.
- **Soft-alpha unmix**, not a hard threshold: solve for the true color under a
  partially-covered pixel so **antialiased fur, thin outlines, and hair strands
  survive**. A naive threshold peels them off — this is the #1 visible tell of a
  cheap pipeline.
- **De-fringe**: kill residual chroma halo in the alpha border.
- For non-chroma inputs, fall back to matting: **BiRefNet** (and
  **BiRefNet-ToonOut**, 95.3% → **99.5%** pixel accuracy on anime/illustration —
  the right variant for stylized art), **SAM 2/3**, or `rembg` (16 backends, one
  CLI/HTTP/Python interface). **Licensing:** BRIA **RMBG-2.0 requires a paid
  commercial license** — prefer BiRefNet variants for a commercial game.

**[5] Normalize.**
- **Trim** each frame to its alpha bounding box, then re-place on a **common
  canvas** so every frame of a state shares dimensions.
- **Anchor/pivot**: derive from the silhouette — the ground-contact point
  (lowest opaque row, x-centroid of contact pixels) — then **stabilize it across
  the whole state** so the character doesn't jitter or slide. Then apply a
  **scale normalization** so a cow and a rancher share one world scale. This is
  Makko's Alignment Editor, and their "floating or sinking" bug is exactly what
  skipping it produces.
- **Palette lock**: build a master palette from the reference image, quantize
  every frame to it. Prevents **palette flicker**, which reads as a glitch in
  motion.
- **Deflicker**: detect pixels that barely change across the loop, lock them to
  one value; drop single-frame outliers via a temporal median.
- **Loop closure**: for cyclic states (walk/run/idle), verify frame N → frame 1
  reads continuously.

**[6] Cull & pack.**
- **Key-pose culling** — Makko's own advice, and it's right: 4–8 frames for a
  walk cycle, 2–3 for an idle. Fewer, better-chosen frames beat 30 interpolated
  ones.
- Optional **RIFE** interpolation to add frames — but only *before* palette lock,
  and never for pixel art (it blends colors and drops alpha).
- Pack to a **power-of-2 atlas** (512/1024/2048) for GPU efficiency. **PNG with
  alpha, always — never JPEG.**
- Emit a manifest (§6). Emit **per-state GIF + contact sheet for QA** — judge
  motion as motion before anything ships.

---

## 6. Output contract

Every generated asset is a directory. The manifest is the interface between the
tool and any engine — design it once, properly.

```
cow_hereford/
  sheet.png              # power-of-2 atlas, PNG + true alpha
  manifest.json          # our format (below)
  sheet.aseprite.json    # Aseprite json-array export, for tool compatibility
  qa/
    walk_s.gif  idle_s.gif  contact_sheet.png
    provenance.json       # model, prompt, seed, refs, pipeline version, cost
```

```jsonc
{
  "schemaVersion": 1,
  "id": "cow_hereford",
  "sheet": "sheet.png",
  "sheetSize": [1024, 1024],
  "pixelsPerUnit": 64,
  "states": {
    "walk_s": {
      "fps": 12, "loop": true,
      "frames": [
        // absolute rect in the atlas + normalized pivot (0,0 = bottom-left)
        { "rect": [0, 0, 128, 128], "pivot": [0.5, 0.08], "durationMs": 83 }
      ]
    }
  }
}
```

Design notes:
- **Absolute frame rects**, not "cell 3 of a grid" — survives repacking.
- **Per-frame pivot**, normalized. Per-frame (not per-sheet) is what kills
  sub-pixel jitter.
- **Per-state fps + loop flags** in the data, so the engine importer needs no
  hand-authoring.
- `durationMs` per frame allows non-uniform timing (hold the anticipation frame).
- Also emit **Aseprite `json-array`** (`aseprite -b sprite.ase --format
  json-array --data spritesheet.json --sheet spritesheet.png`) — it's the de
  facto interchange format, so existing parsers and hand-editing in Aseprite
  both work for free.
- `provenance.json` records model + prompt + seed + refs + cost. Non-optional:
  it's how we regenerate one asset in a consistent style six months from now,
  and how we audit commercial-rights posture per asset.

### Unity importer (fits `unity/`, Unity 6 LTS + URP 2D)

An `AssetPostprocessor` that, when a `sheet.png` lands next to a `manifest.json`:
reads the manifest, builds a `SpriteMetaData[]` (rect + name + pivot per frame),
assigns the sprite sheet, `Apply()`; sets `spritePixelsPerUnit` from the
manifest; then generates one **`AnimationClip` per state** at the manifest's fps
with the loop flag set, and optionally an `AnimatorController`. Zero manual
slicing, ever. `pivot (0.5, 0.08)` + correct PPU is precisely the fix for
"floating or sinking."

---

## 7. Standalone program — proposed architecture

```
spritestudio/
  core/          # deterministic, no network, no UI — the real asset
    alpha/       chroma unmix, de-fringe, matting adapters
    normalize/   trim, canvas, anchor solve, scale, palette lock, deflicker
    pack/        atlas layout, manifest emit, Aseprite emit, GIF/QA
    cache/       content-hash addressed; identical input ⇒ zero re-spend
  providers/     one interface, many backends
    identity.py  NanoBanana | Flux2 | QwenEdit | RetroDiffusion | Local(ComfyUI)
    motion.py    GridImage(A) | ImageToVideo(B) | PoseConditioned(C)
    matting.py   BiRefNet | SAM3 | rembg
  project/       Collection: refs (≤16), style anchors, palette, characters, states
  cli/           spritestudio animate --ref cow.png --states walk,idle --dirs 8
  ui/            local web UI (FastAPI + SPA): drop image, pick states, review, tweak anchors
  exporters/     unity/ (AssetPostprocessor + manifest), godot/, raw/
```

**Language: Python for `core` + `providers`.** The whole matting /
interpolation / quantization ecosystem is Python (Pillow, NumPy, OpenCV,
onnxruntime, rembg), and every model provider ships a Python SDK first. A C#
build would mean reimplementing that against ONNX Runtime for no gain — and this
tool is *not* part of the deterministic sim, so it doesn't inherit the
netstandard2.1 constraint. Only the **Unity importer** is C#, and it belongs in
`unity/` anyway. Ship as a CLI first; the local web UI is a thin layer over it.

**Three rules worth enforcing from day one**

1. **`core` never talks to the network.** It is pure, deterministic, and
   unit-testable with checked-in fixture PNGs — the same discipline as
   `CattleRanch.Sim`. Providers are the only impure layer, behind an interface,
   so a model deprecation (see: Sora 2) is a one-file change.
2. **Cache everything by content hash.** API calls are the only real cost.
   Re-running the pipeline after tweaking a palette must cost $0.
3. **Every stage dumps intermediates.** When a sprite looks wrong you need to
   know whether it was the model, the keying, or the anchor solve.

### Cost model

| Path | Per animation state | Notes |
|---|---|---|
| Makko Starter | **≈$0.90** | 45 cr; $20/mo floor; ~22/mo cap |
| **A — grid, Nano Banana 2** | **≈$0.07–0.15** | 1–2 calls @ $0.067 + retries |
| A — grid, Nano Banana Pro | ≈$0.15–0.30 | $0.134/img |
| **B — I2V (Wan 2.5 @ $0.05/s)** | **≈$0.25–0.40** | ~5 s clip |
| B — I2V (Kling 3.0 Pro) | ≈$0.85 | $0.168/s |
| **C — local (Wan VACE / ComfyUI)** | **$0.00** | GPU + electricity; needs hardware |
| 3D→2D render | $0.00 | after modeling/rigging cost |

A full cattle set — 4 breeds × 5 states × 8 directions — is ~160 states.
Strategy A: **≈$11–24 total.** On Makko: ~7,200 credits ≈ **$100+** and several
months of credit allowance. Post-processing reruns are free in both our columns
and paid in theirs.

---

## 8. Milestones

Each one ends in something verifiable, cheapest-proof-first.

- **M0 — Ground truth ($0).** Free Makko tier, one character, keep every
  artifact. Answers all the Low-confidence rows in §0. Also spend $2 of API
  credit hand-running Strategy A once, by hand, to see raw model output before
  writing any code.
- **M1 — `core` on fixtures (no AI).** Feed hand-made chroma-background frames
  through stages 4–6. Deliverable: `sheet.png` + `manifest.json` + QA GIF, with
  unit tests on soft-alpha unmix, anchor stability, and palette lock. **The
  hardest part is here and it needs no model at all.**
- **M2 — Unity importer.** M1's output auto-slices in Unity 6 with correct
  pivots + PPU and generates AnimationClips. Deliverable: one animated sprite in
  the URP 2D scene, feet on the ground. *This is the moment the tool becomes
  real.*
- **M3 — Strategy A end to end.** `spritestudio animate --ref cow.png --states
  walk,idle` → M1 → M2. Identity lock (stage 2) + grid motion + content-hash
  cache + `provenance.json`.
- **M4 — Collections + 8 directions.** Persisted style anchors (≤16 refs, ≤3 per
  call), master palette, 8-direction isometric turnarounds. This is what makes
  output look like *one game* instead of one prompt.
- **M5 — Local web UI + Strategy B.** Drop-an-image UI, side-by-side takes,
  manual anchor nudge (our Alignment Editor), I2V behind the same provider
  interface. Optional **M6**: Strategy C local pose-conditioned studio mode, or
  the 3D→2D path.

## 9. Risks & open questions

1. **Multi-directional consistency is the real hard problem.** A cow from 8
   isometric angles, still the same cow, is where diffusion is weakest and where
   our isometric camera is most demanding. Test this at M0 before committing to
   the AI path — 3D→2D may simply win here.
2. **Alpha quality on fur/thin outlines.** Cattle are fluffy silhouettes.
   Soft-alpha unmix + BiRefNet-ToonOut must be validated on a real cow at M1.
3. **Model churn.** Sora 2's API dies 2026-09-24; model names/prices in §3 and
   §5 will move. Mitigated by the provider interface — never let a model name
   reach `core`.
4. **Commercial rights.** Makko grants full commercial rights. Building our own
   means we inherit **each provider's** terms per asset, plus any training-data
   exposure. Retro Diffusion advertises consensually-licensed training data;
   BRIA RMBG-2.0 needs a paid license. `provenance.json` exists so this is
   auditable per asset rather than a guess.
5. **Scope honesty.** Makko's game-builder half is explicitly out of scope. If
   what we actually want is "a few hundred consistent cattle frames," the
   fastest path may be M0–M3 plus the 3D→2D renderer — and never a UI at all.

**Open question for the owner:** target art style? *True pixel art* (→ Retro
Diffusion, small canvases, hard palette lock) vs *hand-drawn/painted 2.5D*
(→ Nano Banana / FLUX.2, high-res, softer alpha) changes model choice, canvas
sizes, and roughly half of stage 5. `D13` chose a hand-drawn SVG look for the
web prototype, which hints at the latter — but that was a prototype decision,
not an art direction.

---

## Sources

Makko: [makko.ai](https://www.makko.ai/) ·
[launch PR](https://www.globenewswire.com/news-release/2026/04/20/3276723/0/en/makko-launches-ai-powered-2d-game-studio-with-over-40-000-game-assets-created-in-beta.html) ·
[Product Hunt](https://www.producthunt.com/products/makko-ai) ·
[sprite animation workflow / anchor points](https://blog.makko.ai/sprite-animation-workflow-asset-hygiene-anchor-points-and-getting-game-characters-right/) ·
[Art Studio](https://blog.makko.ai/what-is-makko-art-studio-the-ai-game-asset-generator-built-for-game-developers/) ·
[AI game art generator](https://blog.makko.ai/ai-game-art-generator/) ·
[Collections guide](https://blog.makko.ai/consistent-ai-game-art-makko-collections-guide/) ·
[alignment→hitboxes pipeline](https://blog.makko.ai/from-alignment-to-hitboxes-the-full-2d-pixel-art-character-pipeline-week-of-march-24/) ·
[Collections release notes](https://www.makko.ai/news/collections-tiered-ai-march-2026)

Reviews/pricing: [MakerStack](https://makerstack.co/reviews/makko-ai-review/) ·
[websites2know pricing](https://websites2know.com/makko-ai-pricing-explained/) ·
[websites2know review](https://websites2know.com/makko-ai-review-is-it-legit-or-just-overhyped/) ·
[aipure](https://aipure.ai/products/makko-ai) ·
[aitooldiscovery](https://www.aitooldiscovery.com/tools/des_makko_ai)

Technique: [Sprite Sheet Diffusion (arXiv 2412.03685)](https://arxiv.org/abs/2412.03685) ·
[SSD code](https://github.com/chenganhsieh/Sprite-Sheet-Diffusion) ·
[aldegad/sprite-gen](https://github.com/aldegad/sprite-gen) ·
[Wan 2.2 VACE pose workflow](https://www.runcomfy.com/comfyui-workflows/wan-2-2-vace-in-comfyui-pose-driven-motion-video-workflow) ·
[Wan2.2 Animate (ComfyUI docs)](https://docs.comfy.org/tutorials/video/wan/wan2-2-animate) ·
[Practical-RIFE](https://github.com/hzwer/practical-rife) ·
[RIFE + pixel art palette/alpha caveats](https://itch.io/t/1300992/proper-way-to-interpolate-pixel-art) ·
[animated pixel art with Python](https://sarthakmishra.com/blog/building-animated-sprite-hero)

Matting: [ComfyUI-RMBG](https://github.com/1038lab/ComfyUI-RMBG) ·
[BiRefNet-ToonOut](https://hackernoon.com/birefnet-toonout-anime-background-removal-with-995percent-accuracy) ·
[rembg](https://pypi.org/project/rembg/) ·
[production BG-removal pipelines](https://www.bestaiweb.ai/how-to-build-a-production-background-removal-pipeline-with-bria-rmbg-2-0-photoroom-api-and-rembg-in-2026/)

Models/pricing: [Nano Banana Pro API pricing](https://pricepertoken.com/pricing-page/model/google-gemini-3-pro-image-preview) ·
[OpenRouter Gemini 3 Pro Image](https://openrouter.ai/google/gemini-3-pro-image) ·
[Qwen vs FLUX vs Nano Banana](https://wavespeed.ai/blog/posts/blog-qwen-image-2-0-vs-flux-nano-banana-pro-comparison/) ·
[AI video API costs, July 2026](https://www.buildmvpfast.com/api-costs/ai-video) ·
[I2V model comparison](https://www.atlascloud.ai/blog/guides/ai-image-to-video-models-compared) ·
[image+video API pricing](https://www.teamday.ai/blog/ai-api-pricing-comparison-2026)

Competitors: [PixelLab](https://www.pixellab.ai/) ·
[PixelLab 8-rotations](https://www.pixellab.ai/docs/tools/create-8-rotations-pro) ·
[Retro Diffusion](https://retrodiffusion.ai/) ·
[Retro Diffusion on Scenario](https://docs.scenario.com/get-started/generation/third-party-model-generation/third-party-model-generation-retro-diffusion) ·
[Scenario 8-direction](https://www.scenario.com/apps/8-direction-sprite-generator) ·
[Spriteflow](https://spriteflow.io/direction-sprite-generator) ·
[Ludo.ai sprite rotation](https://ludo.ai/tools/sprite-rotation-generator) ·
[best AI sprite generators](https://ludo.ai/compare/best-ai-sprite-generators) ·
[best AI pixel art generators](https://blog.mage.space/article/best-ai-pixel-art-generators-2026/83330b2b-607d-4ef3-bca0-19e8ef307e2e) ·
[Sorceress 3D→2D](https://sorceress.games/pages/3d-to-2d) ·
[perfectpixel-studio](https://github.com/gykim80/perfectpixel-studio)

Formats/engine: [Aseprite CLI](https://www.aseprite.org/docs/cli/) ·
[Aseprite sprite-sheet docs](https://www.aseprite.org/docs/sprite-sheet/) ·
[Unity automated sprite slicing](https://discussions.unity.com/t/automated-sprite-slicing-on-import/803916) ·
[SpriteMetaData from JSON](https://discussions.unity.com/t/custom-sprite-split-spritemetadata-by-json-issues/854553) ·
[AssetPostprocessor guide](https://medium.com/@januarelsan/automatically-processing-assets-with-assetpostprocessor-in-unity-editor-54e80d353ddc) ·
[SpriteLab: making sheets with AI](https://www.spritelab.dev/guides/how-to-make-sprite-sheets-with-ai) ·
[sprite sheet animation best practices](https://www.sprite-ai.art/blog/pixel-art-animation)

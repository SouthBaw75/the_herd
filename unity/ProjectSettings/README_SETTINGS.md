# ProjectSettings — set these by hand in the Unity 6 editor

The binary/YAML `ProjectSettings/*.asset` files are **not** generated here on
purpose: they corrupt easily when hand-authored blind, and Unity writes them
correctly the moment you open the project and toggle these settings. This file
is the checklist. Everything below is unverified against a real editor — treat
as "the settings to confirm", not "settings already applied".

Editor version target: **Unity 6 LTS (6000.x)** — decision D2. Use the latest
6000.0 LTS patch you have installed.

## 1. Rendering — URP 2D (decision D2)

1. `Assets > Create > Rendering > URP Asset (with 2D Renderer)`. This creates a
   `UniversalRenderPipelineAsset` **and** a `Renderer2D` data asset. `VERIFY`:
   exact menu wording in 6000.x — it may read "URP Universal Renderer" vs "2D
   Renderer"; you want the **2D Renderer**.
2. `Edit > Project Settings > Graphics` → set **Scriptable Render Pipeline
   Settings** to the URP asset you just made.
3. `Edit > Project Settings > Quality` → for each quality level, set **Render
   Pipeline Asset** to the same URP asset (otherwise a level falls back to
   built-in and 2D lighting/sorting misbehaves).

## 2. Color space — Linear

`Edit > Project Settings > Player > Other Settings > Color Space = Linear`.
(Gamma is the legacy default; Linear is correct for URP.)

## 3. Scripting backend & API compatibility (must match the netstandard2.1 sim, D4)

`Edit > Project Settings > Player > Other Settings`:

- **Api Compatibility Level = .NET Standard 2.1**  ← REQUIRED. The sim is
  `netstandard2.1` (D4); this level lets the Unity assemblies reference the sim
  DLL. Do **not** set ".NET Framework".
- **Scripting Backend**: **Mono** for fast Phase 0 iteration in the editor and
  desktop dev builds. Switch to **IL2CPP** for shipping/standalone player
  builds. `VERIFY`: editor Play mode always uses Mono regardless.
- **Managed Stripping Level**: Minimal (or Low) while prototyping, so Newtonsoft
  reflection-based (de)serialization of `RanchState` is not stripped. See D5 /
  the open question in DECISIONS.md about `decimal Cash` under IL2CPP — confirm
  save/load round-trips before relying on it in a player build.

## 4. Input — required for the Phase 0 clickable-animal + keypress test

`Edit > Project Settings > Player > Other Settings > Active Input Handling =
**Both**`.

Why "Both": the Phase 0 stubs use legacy `UnityEngine.Input.GetKeyDown`
(advance-day key) and `OnMouseDown` (click the placeholder animal). Those only
fire when the legacy Input Manager is active. "Both" keeps the new Input System
package (installed via manifest) available for later while not breaking the
placeholders. Changing this triggers an editor restart.

## 5. Referencing the sim assembly

Not a ProjectSettings toggle — see `Assets/CattleRanch/README_ASMDEF.md`.
Summary: build `CattleRanch.Sim.dll` and drop it in
`Assets/CattleRanch/Plugins/`; it auto-references into `CattleRanch.Game`.

## 6. Newtonsoft (D5)

Provided by the `com.unity.nuget.newtonsoft-json` UPM package (already in
`Packages/manifest.json`). No manual step beyond letting the package resolve on
first open. Do not also import a loose `Newtonsoft.Json.dll` — that causes
duplicate-assembly errors.

# Assembly wiring — how the Unity layer references the sim

**Status:** authored blind (no Unity editor in this container). One manual
step required in-editor, described at the bottom. Anything I could not verify is
flagged `VERIFY`.

## The two assemblies

| Assembly | Where | Target | Purpose |
|---|---|---|---|
| `CattleRanch.Sim` | `/sim/src/CattleRanch.Sim` | `netstandard2.1`, **zero UnityEngine** | authoritative game logic (D4) |
| `CattleRanch.Game` | `unity/Assets/CattleRanch/Runtime/CattleRanch.Game.asmdef` | Unity runtime | presentation stubs that **read** the sim |

Non-negotiable #1 (ARCHITECTURE.md): the sim never references UnityEngine and
Unity never owns game state. Keeping them in two separate assemblies enforces
that at the compiler level — `CattleRanch.Sim` cannot accidentally gain a
UnityEngine dependency, and the Unity code can only touch the sim's public API.

## Chosen approach: reference the sim as a precompiled managed plugin (DLL)

We build `CattleRanch.Sim.dll` with `dotnet` and drop it into
`unity/Assets/CattleRanch/Plugins/`. Unity treats managed DLLs there as a
precompiled assembly, and because `CattleRanch.Game.asmdef` has
`"overrideReferences": false`, it is **auto-referenced** — no per-DLL wiring in
the asmdef inspector.

### Why this and not the alternatives

- **Why not "add the sim source into `Assets/`"** — an `.asmdef` only compiles
  `.cs` files inside its own folder tree, so live-referencing the sim source
  would mean copying/symlinking `sim/src` under `unity/Assets`, or adding a
  `package.json` + `.asmdef` **inside `sim/`**. This task forbids touching
  `sim/`, and duplicating source invites drift. Rejected.
- **Why not a local UPM package (`"file:../../sim/..."`)** — also requires a
  `package.json` + `.asmdef` authored **inside `sim/`**. Forbidden here.
- **Why the DLL wins** — it respects "do not touch `sim/`", is portable across
  machines and CI, survives IL2CPP/AOT builds, and the sim's Newtonsoft
  dependency (D5) is satisfied at runtime by the `com.unity.nuget.newtonsoft-json`
  UPM package rather than a second shipped DLL.
- **The one cost** — you must rebuild + copy the DLL whenever the sim changes.
  A one-line build step (below) makes that painless. Acceptable for Phase 0/1
  where the sim churns; revisit if it becomes annoying.

## The manual step (do this in a terminal, once, then whenever the sim changes)

```bash
# from repo root
dotnet build sim/src/CattleRanch.Sim/CattleRanch.Sim.csproj -c Release
cp sim/src/CattleRanch.Sim/bin/Release/netstandard2.1/CattleRanch.Sim.dll \
   unity/Assets/CattleRanch/Plugins/CattleRanch.Sim.dll
```

Then in Unity: the DLL imports automatically. Select it in the Project window and
in the Inspector confirm **Any Platform** is ticked (default). `VERIFY`: no extra
"Assembly Definition References" entry is needed as long as the Game asmdef keeps
`overrideReferences=false`.

> Consider committing the built DLL, or adding the two commands above as a build
> step / git hook, so a fresh clone can open the Unity project without a broken
> reference. (`.gitignore` currently ignores `sim/**/bin`, so the DLL under
> `unity/Assets/CattleRanch/Plugins/` — outside `sim/` — is NOT ignored and will
> be tracked. Decide with the lead whether to commit it or generate it.)

## `CattleRanch.Game.asmdef` reference list

Currently references only `Unity.TextMeshPro` (for `GameClockView`).
`VERIFY`: assembly name `Unity.TextMeshPro` is correct for Unity 6's
`com.unity.ugui` 2.0.0. If you switch the date label from TMP to
`UnityEngine.UI.Text`, drop that reference. The scripts use legacy
`UnityEngine.Input` / `OnMouseDown`, which need **no** asmdef reference but do
need "Active Input Handling = Both" (see `ProjectSettings/README_SETTINGS.md`).

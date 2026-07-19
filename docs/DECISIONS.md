# Decisions Log

Settled choices. Do not relitigate without a new entry that supersedes an old one.

| # | Date | Decision | Rationale |
|---|------|----------|-----------|
| D1 | 2026-07-19 | **Sim-core-first sequencing** for Phase 0 in the headless build env. Lead with the Unity-agnostic simulation + real unit tests; defer editor-only rendering to a track the product owner verifies in Unity. | The cloud container has no Unity editor but does have .NET 8. The sim core is the only part verifiable here, and §8 names the grass curve the true priority. Product owner approved. |
| D2 | 2026-07-19 | **Unity 6 LTS (6000.x) + URP 2D** is the Unity target. | Current LTS line in 2026; best forward support for a new project. Product owner approved. |
| D3 | 2026-07-19 | **Seeded PRNG injected via `IRandom`**; systems never call `System.Random`/`UnityEngine.Random` directly. | Required for the deterministic tick sim (design doc §6) and for reproducible tests. |
| D4 | 2026-07-19 | **Sim core targets `netstandard2.1`, zero `UnityEngine` refs.** Tests target `net8.0`. | Keeps the sim Unity-compatible (Unity supports netstandard2.1) while letting tests run on a modern headless runtime. |
| D5 | 2026-07-19 | **Newtonsoft Json.NET** for `RanchState` save/load (over `JsonUtility`). | Handles dictionaries, polymorphism, and private setters; testable headlessly; Unity package `com.unity.nuget.newtonsoft-json` exists. |
| D6 | 2026-07-19 | **Tick = 1 day; 28 days/season; 112 days/year** (config constants, not literals). | Simple, even seasons; deterministic; tunable via config. |

| D7 | 2026-07-19 | Unity references the sim as a **precompiled DLL** in `Assets/CattleRanch/Plugins/`, **generated locally** (never committed; gitignored). One-command sync: `unity/sync-sim.sh`. | Keeps `sim/` free of Unity files, enforces the sim/presentation split at the compiler level, avoids binary churn in git. Revisit if the rebuild step becomes friction. |

| D8 | 2026-07-19 | Save/load goes through **`RanchSave.ToJson/FromJson`** (in `CattleRanch.Sim.Persistence`) — a single entry point owning the serializer settings, with a contract resolver that writes non-public setters and a converter storing `GameDate` as bare `TotalDays`. No `[JsonProperty]` attributes on domain types; `internal set` encapsulation stays. | Json.NET's default contract silently skips internal setters — a load restored default values (caught in lead verification, then fixed + regression-tested). Centralizing policy prevents consumers from misconfiguring a serializer and quietly corrupting saves. |

## Open questions (raise before they block)
- Cross-**runtime** determinism of `NextGaussian` (Box-Muller uses `Math.Log/Cos`;
  transcendentals are deterministic on a given runtime but not guaranteed
  bit-identical across runtimes/architectures). Irrelevant while saves stay
  on-device; revisit only if cross-platform save sharing or replays ship.
- Exact daily intake (kg DM/head) and carrying-capacity formula constants — will
  be set as *tunable config* in Phase 1 balancing, not hardcoded.
- Serialization of `decimal Cash` under Newtonsoft in Unity's IL2CPP — verify in
  the editor track before relying on it for saves.

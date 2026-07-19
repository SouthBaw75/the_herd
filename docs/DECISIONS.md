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

| D9 | 2026-07-19 | The **Phase 1 fun gate runs on an ugly playable terminal prototype** built on the real sim (no Unity required). Unity editor work (incl. the editor half of the Phase 0 exit test) is deferred to Phase 3, where the owner either follows beginner guides or a local Claude Code session drives the editor. | Product owner has never used Unity and this environment cannot run the editor. The doc's gate requires proving the loop is fun in ugly form — presentation-agnostic by our own architecture. Accepted risk: §6 iso-sorting learning risk deferred (presentation-only). Product owner approved. |
| D10 | 2026-07-19 | `RanchState` owns a serializable `Rng` (`DeterministicRandom` with public-get state); `DeterministicRandom` drops the Box-Muller spare-value cache so its entire state is one `ulong`. Systems consume randomness only via this stream, in fixed engine order. | Post-load simulation must continue bit-identically to a never-saved run. A hidden Gaussian cache would silently desync streams across save/load. One wasted uniform draw per Gaussian is a negligible cost. |

| D11 | 2026-07-19 | **Amends D9:** the Phase 0 Unity editor exit test moves back up — the owner (Unity 6.5 now installed) sets up the starter scene NOW with guided help, alongside (not instead of) the terminal fun-gate playtest. Art/gameplay in Unity remains Phase 3; the fun gate remains binding. | Owner wants something visual; the Phase 0 scene is already-sanctioned work that provides it without violating the prove-the-fun-first gate. |

| D12 | 2026-07-19 | The fun-gate prototype gets a **browser version: Blazor WebAssembly** (`web/`) compiling the unmodified `CattleRanch.Sim` to WASM. The terminal prototype stays; Unity remains the product target (paused at the owner's request, resumed later). NO parallel JS sim — the C# sim stays the single source of truth in every frontend. | Owner found both the terminal and the Unity editor unfriendly; a click-and-play web UI serves the fun gate better for this playtester. WASM feasibility verified in-container (template + runtime packs + static publish all work). |

## Open questions (raise before they block)
- Cross-**runtime** determinism of `NextGaussian` (Box-Muller uses `Math.Log/Cos`;
  transcendentals are deterministic on a given runtime but not guaranteed
  bit-identical across runtimes/architectures). Irrelevant while saves stay
  on-device; revisit only if cross-platform save sharing or replays ship.
- Exact daily intake (kg DM/head) and carrying-capacity formula constants — will
  be set as *tunable config* in Phase 1 balancing, not hardcoded.
- Serialization of `decimal Cash` under Newtonsoft in Unity's IL2CPP — verify in
  the editor track before relying on it for saves.

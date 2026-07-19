# Cattle Ranch — Architecture Contract

This is the **interface contract** the simulation code implements against, and
the standard every agent's work is reviewed against. It operationalizes §6 of
the design doc. If code violates this, it is rejected even if it "works."

## Non-negotiables (from design doc §6)

1. **Simulation is separated from presentation.** The `CattleRanch.Sim` assembly
   has **zero** `UnityEngine` references. It is a plain `netstandard2.1` library
   that compiles and unit-tests headlessly. Unity code *reads from* the sim; it
   never owns game state.
2. **Deterministic, tick-based clock.** The atomic tick is **one day**. Systems
   update on day and season boundaries — never in Unity's frame `Update()`.
   Given the same seed and the same inputs, a run is bit-for-bit reproducible.
3. **Seeded RNG, injected.** All randomness flows through an injected
   `IRandom` (see below). `System.Random`/`UnityEngine.Random` are **never**
   called directly inside systems. This is what makes #2 testable.
4. **Data-driven content.** Breeds, traits, events, and market curves are
   *data*, not hardcoded branches. In the headless core they are plain
   config/POCO objects; in Unity they are authored as `ScriptableObject`s that
   deserialize into those same POCOs. Tuning must not require code edits.
5. **Serializable root.** `RanchState` is the serializable root that owns all
   game data. Save/load = serialize/deserialize `RanchState` (Newtonsoft
   Json.NET). No game state lives outside it.

## Assembly / project layout

```
sim/
  CattleRanch.Sim.sln
  src/CattleRanch.Sim/          netstandard2.1, no UnityEngine
  tests/CattleRanch.Sim.Tests/  net8.0, xUnit — the verification instrument
unity/                          Unity 6 LTS (URP 2D) project — reads the sim
docs/                           this contract, decisions log
```

## Core types (Phase 0 / Phase 1 surface)

Namespaces: `CattleRanch.Sim` (root types), `CattleRanch.Sim.Systems`,
`CattleRanch.Sim.Math`.

### Randomness
```csharp
public interface IRandom {
    int NextInt(int minInclusive, int maxExclusive);
    double NextDouble();                 // [0,1)
    double NextGaussian(double mean, double stdDev);
}
// Deterministic implementation seeded from a long. Same seed => same stream.
public sealed class DeterministicRandom : IRandom { public DeterministicRandom(long seed); }
```

### Clock
```csharp
public enum Season { Spring, Summer, Fall, Winter }

public sealed class GameDate {          // value-like; Day 0 == Year 1, Spring, Day 1
    public int TotalDays { get; }        // monotonic tick counter
    public int Year { get; }             // 1-based
    public Season Season { get; }
    public int DayOfSeason { get; }      // 1-based within season
}

public sealed class GameClock {
    public GameDate Date { get; }
    // Advances exactly one day. Returns events crossed (e.g. SeasonChanged).
    public event Action<Season> SeasonChanged;
    public event Action<int>    YearChanged;
    public void Tick();                  // advance 1 day
    public void Advance(int days);       // Tick() * days
}
```
Season length is a config constant (default **28 days/season → 112 days/year**),
sourced from config, not a magic literal buried in logic.

### Land — the "heartbeat" (design doc §8.3)
```csharp
public sealed class Paddock {
    public string Id { get; }
    public double AreaHectares { get; }
    public double GrassBiomass { get; }   // kg dry matter (DM) available
    public double SoilHealth { get; }     // 0..1 multiplier on regrowth
    public double CarryingCapacity { get; } // biomass ceiling (fn of soil, area)
    // occupants tracked by Animal Ids
}
```
**Regrowth model (logistic, deterministic):** each day, ungrazed biomass grows
toward `CarryingCapacity` on a logistic curve modulated by `SoilHealth` and a
seasonal growth factor (winter ≈ 0). Grazing subtracts intake
(`headCount * dailyIntakeKg`). **Sustained overstock** (intake > regrowth) drives
biomass down and, once biomass stays below a threshold, **degrades SoilHealth**,
lowering future `CarryingCapacity`. Rested paddocks recover biomass, then soil.
This asymmetry (fast to strip, slow to heal) is the core discipline of the game.

### Herd
```csharp
public struct Genome {                    // heritable traits, 0..100 floats
    public float GrowthRate, CalvingEase, Marbling, Mothering,
                 HeatTolerance, DiseaseResistance, Fertility;
}
public enum Sex { Female, Male }
public sealed class Animal {
    public string Id { get; }
    public string Name { get; }           // optional
    public Sex Sex { get; }
    public string Breed { get; }
    public int AgeDays { get; }
    public double WeightKg { get; }
    public Genome Genome { get; }
    public HealthState Health { get; }
    public PregnancyState Pregnancy { get; }
}
```

### Root
```csharp
public sealed class RanchState {          // the serializable root
    public long Seed { get; }
    public GameDate Date { get; }
    public decimal Cash { get; }
    public List<Animal> Herd { get; }
    public List<Paddock> Paddocks { get; }
    // Market / Weather / Event system state added in later phases.
}
```

## Determinism rules
- No `DateTime.Now`, no wall-clock, no `Guid.NewGuid()` inside sim logic.
- Iterate collections in a defined order; never rely on hash-set ordering for
  anything that affects the sim.
- All floating-point sim math is `double`; genome trait storage is `float`.
- Ids are assigned from a deterministic counter seeded into `RanchState`.

## Definition of Done (every task)
Meets acceptance criteria **and** compiles (`dotnet build`) **and** tests pass
(`dotnet test`, where applicable) **and** adheres to this contract **and** has
been read line-by-line in review. All five, or it is not done.

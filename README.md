# Cattle Ranch

A mid-depth, 2.5D isometric cattle-ranching simulation. Solo build in Unity (C#).

> **One-line pitch:** *Stardew Valley's warmth meets a real cattle economy — grow a
> herd, read the land, beat the market.*

See [`docs/`](docs/) for the design doc, the [architecture contract](docs/ARCHITECTURE.md),
and the [decisions log](docs/DECISIONS.md).

## Repository layout

```
sim/     Headless, Unity-agnostic simulation core (netstandard2.1) + xUnit tests.
         This is the deterministic heart of the game. Runs and is tested with no
         Unity editor. Build:  cd sim && dotnet build   Test:  dotnet test
unity/   Unity 6 LTS (URP 2D) project — the presentation layer. Reads from the sim.
docs/    Design doc, architecture contract, decisions log.
```

## Architecture in one breath

Simulation is fully separated from presentation. All game state lives in a
serializable `RanchState`; a deterministic, tick-based clock (1 tick = 1 day)
advances the sim; content is data-driven; all randomness is seeded and injected.
The Unity layer only *reads* the sim. See [ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Working with the sim core (headless)

```bash
cd sim
dotnet build      # compile the core + tests
dotnet test       # run the deterministic sim test suite
```

## Play the prototype (no Unity required)

The Phase 1 fun-gate build (D9): an ugly, playable terminal game running on the
real simulation. Weekly decisions — read the herd, the grass, and the market,
then sell or hold and advance time.

```bash
dotnet run --project sim/src/CattleRanch.Play
```

Blank seed = the shared default (1701), so two people can compare identical
runs. Saves go to `ranch-save.json` in the working directory.

## First-time clone

```bash
git lfs install   # art/audio are stored via Git LFS (see .gitattributes)
```

## Build status

Phase 0 (foundations) — in progress. See the decisions log for current state.

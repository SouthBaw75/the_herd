# Plugins — sim assembly drop point

Build `CattleRanch.Sim.dll` and copy it here so `CattleRanch.Game` auto-references
the sim. See `../README_ASMDEF.md` for the exact command and rationale.

```bash
# from repo root
dotnet build sim/src/CattleRanch.Sim/CattleRanch.Sim.csproj -c Release
cp sim/src/CattleRanch.Sim/bin/Release/netstandard2.1/CattleRanch.Sim.dll \
   unity/Assets/CattleRanch/Plugins/CattleRanch.Sim.dll
```

This file also keeps the folder tracked in git before the DLL exists.

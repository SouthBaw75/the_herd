#!/usr/bin/env bash
# Rebuild the headless sim and copy the DLL into the Unity project.
# Run from anywhere; idempotent. See unity/Assets/CattleRanch/README_ASMDEF.md.
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet build "$REPO_ROOT/sim/src/CattleRanch.Sim/CattleRanch.Sim.csproj" -c Release
mkdir -p "$REPO_ROOT/unity/Assets/CattleRanch/Plugins"
cp "$REPO_ROOT/sim/src/CattleRanch.Sim/bin/Release/netstandard2.1/CattleRanch.Sim.dll" \
   "$REPO_ROOT/unity/Assets/CattleRanch/Plugins/CattleRanch.Sim.dll"
echo "Synced CattleRanch.Sim.dll -> unity/Assets/CattleRanch/Plugins/"

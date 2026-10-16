#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 2 ]]; then
  echo 'Usage: bash scripts/check.sh <Designer directory> <separate build directory>' >&2
  exit 2
fi
designer_dir=$(realpath "$1")
build_dir=$(realpath -m "$2")
source_dir=$(realpath "$(dirname "$0")/..")
case "$build_dir/" in "$source_dir/"*) echo 'Use a build directory outside the source checkout.' >&2; exit 2;; esac
cd "$source_dir"
mkdir -p "$build_dir/logs"
dotnet build Designer.Modules.BetterProbesInjectors.csproj -c Release --artifacts-path "$build_dir/wsl" "-p:DesignerDir=$designer_dir" 2>&1 | tee "$build_dir/logs/build.txt"
dotnet run --project tests/Logic -c Release --artifacts-path "$build_dir/logic" 2>&1 | tee "$build_dir/logs/logic.txt"
dotnet run --project tests/HostContract -c Release --artifacts-path "$build_dir/contract" -- "$designer_dir" "$build_dir/wsl/bin/Designer.Modules.BetterProbesInjectors/release/Designer.Modules.BetterProbesInjectors.dll" 2>&1 | tee "$build_dir/logs/contract.txt"
sha256sum "$build_dir/wsl/bin/Designer.Modules.BetterProbesInjectors/release/Designer.Modules.BetterProbesInjectors.dll" | tee "$build_dir/logs/payload.sha256"

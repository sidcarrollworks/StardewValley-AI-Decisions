#!/usr/bin/env bash
# Launch a local Laya server (laya-serve) for the Stardew NPC decision client.
# Uses sidecar/.venv if present, otherwise whatever laya-serve is on PATH.
# Environment variables already set take precedence over these defaults.
set -euo pipefail

export LAYA_HOST="${LAYA_HOST:-127.0.0.1}"   # loopback only; Laya's own default 0.0.0.0 exposes it on the LAN
export LAYA_PORT="${LAYA_PORT:-8000}"
export LAYA_MODELS="${LAYA_MODELS:-typed-decisions}"
export LAYA_PRELOAD="${LAYA_PRELOAD:-1}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -x "$here/.venv/bin/laya-serve" ]; then
    exe="$here/.venv/bin/laya-serve"
elif [ -x "$here/.venv/Scripts/laya-serve.exe" ]; then   # Windows venv under Git Bash
    exe="$here/.venv/Scripts/laya-serve.exe"
elif command -v laya-serve >/dev/null 2>&1; then
    exe="laya-serve"
else
    echo 'laya-serve not found. Create sidecar/.venv and run: python -m pip install "laya[serve]" (see sidecar/README.md).' >&2
    exit 1
fi

echo "Starting Laya on http://$LAYA_HOST:$LAYA_PORT (models: $LAYA_MODELS). First run downloads weights; this can take a while."
exec "$exe"

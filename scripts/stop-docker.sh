#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
echo "Stopping FUTUREM Enterprise..."
docker compose down
echo "Stopped."

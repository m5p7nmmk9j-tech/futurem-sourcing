#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."
echo "Starting FUTUREM Enterprise with Docker Compose..."
docker compose up -d --build
echo ""
echo "FUTUREM Enterprise started."
echo "Web: http://localhost:3000"
echo "API: http://localhost:8080"
echo "MySQL: localhost:3307"

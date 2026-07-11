#!/usr/bin/env bash
set -e
cd "$(dirname "$0")/.."

echo "Checking API"
if [ -f api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj ]; then
  dotnet restore api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj
  dotnet build api/Futurem.Sourcing.Api/Futurem.Sourcing.Api.csproj -c Release
  dotnet test api/Futurem.Sourcing.Api.Tests/Futurem.Sourcing.Api.Tests.csproj -c Release
fi

echo "Checking Web"
if [ -f web/package.json ]; then
  cd web
  npm ci
  npm test
  npm run build
fi

echo "Check completed"

@echo off
cd /d %~dp0\..
echo Checking API
if exist api\Futurem.Sourcing.Api\Futurem.Sourcing.Api.csproj (
  dotnet restore api\Futurem.Sourcing.Api\Futurem.Sourcing.Api.csproj
  dotnet build api\Futurem.Sourcing.Api\Futurem.Sourcing.Api.csproj -c Release
)
echo Checking Web
if exist web\package.json (
  cd web
  npm ci
  npm run build
)
echo Check completed
pause

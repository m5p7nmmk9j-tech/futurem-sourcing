@echo off
cd /d %~dp0\..
echo Starting FUTUREM Enterprise with Docker Compose...
docker compose up -d --build
echo.
echo FUTUREM Enterprise started.
echo Web: http://localhost:3000
echo API: http://localhost:8080
echo MySQL: localhost:3307
pause

@echo off
cd /d %~dp0\..
echo Stopping FUTUREM Enterprise...
docker compose down
echo Stopped.
pause

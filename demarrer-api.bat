@echo off
REM Demarre l'API StockWave (.NET) sur http://localhost:5080
cd /d "%~dp0api-gestion"
echo ============================================
echo   StockWave API - demarrage sur le port 5080
echo ============================================
dotnet run --urls http://localhost:5080
pause

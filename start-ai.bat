@echo off
setlocal ENABLEDELAYEDEXPANSION

echo ================================
echo   Starting AI API Server
echo ================================

REM Go to project root (where this bat is)
cd /d "%~dp0"

REM Go to AI folder
cd apps\ai

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Return to project root
cd ..\..

echo.
echo ================================
echo   AI Server is running...
echo   Press CTRL+C to stop
echo ================================
echo.

REM Run Uvicorn
uvicorn apps.ai.src.api.main:app --reload --port 8001

pause

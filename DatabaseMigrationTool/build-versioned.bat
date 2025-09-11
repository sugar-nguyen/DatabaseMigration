@echo off
echo ========================================
echo   Database Migration Tool - Build Script
echo ========================================
echo.

if "%1"=="" (
    echo [INTERACTIVE MODE]
    powershell -ExecutionPolicy Bypass -File "build-portable-versioned.ps1" -Interactive
) else (
    echo [CUSTOM VERSION: %1]
    powershell -ExecutionPolicy Bypass -File "build-portable-versioned.ps1" -CustomVersion "%1"
)

pause

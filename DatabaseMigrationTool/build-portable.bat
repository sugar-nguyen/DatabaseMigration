@echo off
echo ========================================
echo   Database Migration Tool - Build Script
echo ========================================
echo.

echo [1/3] Cleaning previous build...
if exist "publish-portable" rmdir /s /q "publish-portable"

echo [2/3] Building portable executable...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./publish-portable"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [3/3] Build completed successfully!
    echo.
    echo ========================================
    echo   Build Information
    echo ========================================
    for %%A in ("publish-portable\DatabaseMigrationTool.exe") do (
        echo File: %%~nA%%~xA
        echo Size: %%~zA bytes ^(~%%~zA:~0,-6%% MB^)
        echo Location: %%~dpA
    )
    echo.
    echo ✅ Portable executable ready for distribution!
    echo 📁 Location: .\publish-portable\DatabaseMigrationTool.exe
    echo.
) else (
    echo.
    echo ❌ Build failed! Check error messages above.
    echo.
)

pause

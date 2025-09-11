# Quick Build Scripts for Database Migration Tool

# Build with config version
function Build-Default {
    .\build-portable-versioned.ps1
}

# Build with custom version
function Build-Version {
    param([string]$Version)
    .\build-portable-versioned.ps1 -CustomVersion $Version
}

# Interactive build (ask for version)
function Build-Interactive {
    .\build-portable-versioned.ps1 -Interactive
}

# Usage examples
Write-Host "=== Database Migration Tool - Build Helpers ===" -ForegroundColor Cyan
Write-Host
Write-Host "Available commands:" -ForegroundColor White
Write-Host "  Build-Default       # Use version from config file" -ForegroundColor Gray
Write-Host "  Build-Version '1.5' # Use custom version" -ForegroundColor Gray  
Write-Host "  Build-Interactive   # Ask for version input" -ForegroundColor Gray
Write-Host
Write-Host "Examples:" -ForegroundColor White
Write-Host "  Build-Version '1.2.3'" -ForegroundColor Green
Write-Host "  Build-Version '2.0.0-beta'" -ForegroundColor Green
Write-Host "  Build-Interactive" -ForegroundColor Green
Write-Host

# Database Migration Tool - Build Script with Version Management
param(
    [string]$CustomVersion = $null,
    [switch]$Interactive = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Database Migration Tool - Build Script" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host

# Function to read version config
function Get-VersionConfig {
    if (Test-Path "version.json") {
        try {
            $config = Get-Content "version.json" | ConvertFrom-Json
            return $config
        }
        catch {
            Write-Host "⚠️  Error reading version.json, using defaults" -ForegroundColor Yellow
            return $null
        }
    }
    return $null
}

# Function to update version config
function Update-VersionConfig {
    param($config, $newVersion)
    
    if ($config -and $config.autoIncrement) {
        $config.buildNumber = [int]$config.buildNumber + 1
        $config.version = $newVersion
        $config | ConvertTo-Json -Depth 10 | Set-Content "version.json"
        Write-Host "📝 Updated version.json: $newVersion (Build: $($config.buildNumber))" -ForegroundColor Green
    }
}

# Load version configuration
$versionConfig = Get-VersionConfig

# Determine version to use
$version = $null
if ($CustomVersion) {
    $version = $CustomVersion
    Write-Host "🔧 Using custom version: $version" -ForegroundColor Yellow
}
elseif ($Interactive) {
    if ($versionConfig) {
        Write-Host "📋 Current version: $($versionConfig.version)" -ForegroundColor White
    }
    $userInput = Read-Host "Enter version (or press Enter for auto)"
    if ($userInput) {
        $version = $userInput
    }
}

# Use config version or default
if (-not $version) {
    if ($versionConfig) {
        $version = $versionConfig.version
        Write-Host "📋 Using config version: $version" -ForegroundColor White
    } else {
        $version = "1.0.0"
        Write-Host "⚡ Using default version: $version" -ForegroundColor White
    }
}

# Clean version for filename (remove invalid characters)
$cleanVersion = $version -replace '[\\/:*?"<>|]', '_'
$exeName = "DatabaseMigrationTool_v$cleanVersion"

Write-Host "🚀 Building: $exeName.exe" -ForegroundColor Cyan
Write-Host

Write-Host "[1/4] Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path "publish-portable") {
    Remove-Item "publish-portable" -Recurse -Force
}

Write-Host "[2/4] Updating project version..." -ForegroundColor Yellow
# Update project file with version info
$csprojContent = Get-Content "DatabaseMigrationTool.csproj"
$csprojContent = $csprojContent -replace '<AssemblyVersion>.*</AssemblyVersion>', "<AssemblyVersion>$version.0</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>.*</FileVersion>', "<FileVersion>$version.0</FileVersion>"
$csprojContent = $csprojContent -replace '<AssemblyName>.*</AssemblyName>', "<AssemblyName>$exeName</AssemblyName>"

if ($versionConfig) {
    $csprojContent = $csprojContent -replace '<AssemblyTitle>.*</AssemblyTitle>', "<AssemblyTitle>$($versionConfig.productName)</AssemblyTitle>"
    $csprojContent = $csprojContent -replace '<AssemblyDescription>.*</AssemblyDescription>', "<AssemblyDescription>$($versionConfig.description)</AssemblyDescription>"
    $csprojContent = $csprojContent -replace '<AssemblyCompany>.*</AssemblyCompany>', "<AssemblyCompany>$($versionConfig.companyName)</AssemblyCompany>"
    $csprojContent = $csprojContent -replace '<Copyright>.*</Copyright>', "<Copyright>$($versionConfig.copyright)</Copyright>"
}

$csprojContent | Set-Content "DatabaseMigrationTool.csproj"

Write-Host "[3/4] Building portable executable..." -ForegroundColor Yellow
$output = dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./publish-portable" 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host
    Write-Host "[4/4] Build completed successfully!" -ForegroundColor Green
    Write-Host
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Build Information" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Find the executable file
    $exeFile = Get-ChildItem "publish-portable\*.exe" | Select-Object -First 1
    if ($exeFile) {
        $sizeMB = [math]::Round($exeFile.Length / 1MB, 1)
        
        Write-Host "📁 File: $($exeFile.Name)" -ForegroundColor White
        Write-Host "📏 Size: $($exeFile.Length) bytes (~$sizeMB MB)" -ForegroundColor White
        Write-Host "📍 Location: $($exeFile.DirectoryName)" -ForegroundColor White
        Write-Host "📅 Created: $($exeFile.LastWriteTime)" -ForegroundColor White
        Write-Host "🔖 Version: $version" -ForegroundColor White
        if ($versionConfig) {
            Write-Host "🔢 Build: $($versionConfig.buildNumber)" -ForegroundColor White
        }
        Write-Host
        Write-Host "✅ Portable executable ready for distribution!" -ForegroundColor Green
        Write-Host "📁 Location: .\publish-portable\$($exeFile.Name)" -ForegroundColor Cyan
        
        # Update version config if auto-increment is enabled
        Update-VersionConfig $versionConfig $version
    } else {
        Write-Host "⚠️  No executable found in publish-portable directory" -ForegroundColor Yellow
    }
} else {
    Write-Host
    Write-Host "❌ Build failed! Check error messages above." -ForegroundColor Red
}

Write-Host
Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

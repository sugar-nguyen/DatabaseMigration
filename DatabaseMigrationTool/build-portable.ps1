# Database Migration Tool - Build Script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Database Migration Tool - Build Script" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host

Write-Host "[1/3] Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path "publish-portable") {
    Remove-Item "publish-portable" -Recurse -Force
}

Write-Host "[2/3] Building portable executable..." -ForegroundColor Yellow
$result = dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./publish-portable"

if ($LASTEXITCODE -eq 0) {
    Write-Host
    Write-Host "[3/3] Build completed successfully!" -ForegroundColor Green
    Write-Host
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Build Information" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Find the executable file (handles different assembly names)
    $exeFile = Get-ChildItem "publish-portable\*.exe" | Select-Object -First 1
    if ($exeFile) {
        $sizeMB = [math]::Round($exeFile.Length / 1MB, 1)
        
        Write-Host "File: $($exeFile.Name)" -ForegroundColor White
        Write-Host "Size: $($exeFile.Length) bytes (~$sizeMB MB)" -ForegroundColor White
        Write-Host "Location: $($exeFile.DirectoryName)" -ForegroundColor White
        Write-Host "Created: $($exeFile.LastWriteTime)" -ForegroundColor White
        Write-Host
        Write-Host "✅ Portable executable ready for distribution!" -ForegroundColor Green
        Write-Host "📁 Location: .\publish-portable\$($exeFile.Name)" -ForegroundColor Cyan
    } else {
        Write-Host "⚠️  No executable found in publish-portable directory" -ForegroundColor Yellow
    }
    Write-Host
} else {
    Write-Host
    Write-Host "❌ Build failed! Check error messages above." -ForegroundColor Red
    Write-Host
}

Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

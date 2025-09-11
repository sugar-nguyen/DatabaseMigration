# Icon Verification Script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Icon Verification Script" -ForegroundColor Cyan  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host

$exePath = Get-ChildItem "./publish-portable/*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName

if ($exePath -and (Test-Path $exePath)) {
    Write-Host "✅ Executable found: $($exePath | Split-Path -Leaf)" -ForegroundColor Green
    
    $exe = Get-Item $exePath
    Write-Host "📁 File size: $([math]::Round($exe.Length / 1MB, 1)) MB" -ForegroundColor White
    Write-Host "📅 Created: $($exe.LastWriteTime)" -ForegroundColor White
    
    try {
        Add-Type -AssemblyName System.Drawing
        $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe.FullName)
        Write-Host "🎨 Icon embedded: Yes" -ForegroundColor Green
        Write-Host "📐 Icon size: $($icon.Width)x$($icon.Height)" -ForegroundColor White
        $icon.Dispose()
    }
    catch {
        Write-Host "❌ Icon extraction failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Check version info
    $versionInfo = $exe.VersionInfo
    if ($versionInfo.ProductName) {
        Write-Host "📋 Product: $($versionInfo.ProductName)" -ForegroundColor White
        Write-Host "📋 Version: $($versionInfo.FileVersion)" -ForegroundColor White
    }
    
    Write-Host
    Write-Host "🔄 If icon appears old in Explorer:" -ForegroundColor Yellow
    Write-Host "   1. Press F5 to refresh Explorer" -ForegroundColor Gray
    Write-Host "   2. Clear icon cache: ie4uinit.exe -show" -ForegroundColor Gray
    Write-Host "   3. Restart Explorer process" -ForegroundColor Gray
    Write-Host "   4. Copy file to different location" -ForegroundColor Gray
    
} else {
    Write-Host "❌ Executable not found: $exePath" -ForegroundColor Red
    Write-Host "   Run build-portable.ps1 first" -ForegroundColor Gray
}

Write-Host

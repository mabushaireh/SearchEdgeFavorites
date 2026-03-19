# Diagnostic script to verify Sync Favorites command is registered
# Run this from PowerShell

Write-Host "`n=== Edge Favorites AI - Sync Command Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check project build
Write-Host "[1/5] Checking project build status..." -ForegroundColor Yellow
$projectPath = "D:\personal\dev\SearchEdgeFavorites\SearchEdgeFavorites\SearchEdgeFavorites.csproj"
$buildOutput = "D:\personal\dev\SearchEdgeFavorites\SearchEdgeFavorites\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64"

if (Test-Path $buildOutput) {
    Write-Host "  ✓ Build output found" -ForegroundColor Green
    $dll = Get-ChildItem "$buildOutput\SearchEdgeFavorites.dll" -ErrorAction SilentlyContinue
    if ($dll) {
        Write-Host "  ✓ DLL exists: $($dll.LastWriteTime)" -ForegroundColor Green
    }
} else {
    Write-Host "  ✗ Build output not found - Building now..." -ForegroundColor Red
    Push-Location "D:\personal\dev\SearchEdgeFavorites"
    dotnet build -c Debug
    Pop-Location
}

# 2. Check if commands file exists
Write-Host "`n[2/5] Checking command files..." -ForegroundColor Yellow
$commandsFile = "D:\personal\dev\SearchEdgeFavorites\SearchEdgeFavorites\Pages\SyncFavoritesPage.cs"
if (Test-Path $commandsFile) {
    Write-Host "  ✓ SyncFavoritesPage.cs exists" -ForegroundColor Green
    $content = Get-Content $commandsFile -Raw
    if ($content -match 'class SyncFavoritesCommand') {
        Write-Host "  ✓ SyncFavoritesCommand class found" -ForegroundColor Green
    }
} else {
    Write-Host "  ✗ SyncFavoritesPage.cs NOT FOUND" -ForegroundColor Red
}

# 3. Check CommandsProvider registration
Write-Host "`n[3/5] Checking command registration..." -ForegroundColor Yellow
$providerFile = "D:\personal\dev\SearchEdgeFavorites\SearchEdgeFavorites\SearchEdgeFavoritesCommandsProvider.cs"
$providerContent = Get-Content $providerFile -Raw
if ($providerContent -match 'new CommandItem\(new SyncFavoritesCommand\(\)\)') {
    Write-Host "  ✓ Sync command registered in provider" -ForegroundColor Green
} else {
    Write-Host "  ✗ Sync command NOT registered" -ForegroundColor Red
}

# 4. Check log directory
Write-Host "`n[4/5] Checking application data..." -ForegroundColor Yellow
$appData = "$env:LOCALAPPDATA\SearchEdgeFavorites"
if (Test-Path $appData) {
    Write-Host "  ✓ App data directory exists: $appData" -ForegroundColor Green
    $logs = Get-ChildItem "$appData\*.log" -ErrorAction SilentlyContinue
    if ($logs) {
        Write-Host "  ✓ Found $($logs.Count) log file(s):" -ForegroundColor Green
        foreach ($log in $logs) {
            Write-Host "    - $($log.Name) ($('{0:N2}' -f ($log.Length/1KB)) KB, $($log.LastWriteTime))"
        }
    }
} else {
    Write-Host "  ⓘ App data directory will be created on first run" -ForegroundColor Cyan
}

# 5. Provide next steps
Write-Host "`n[5/5] Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Rebuild the solution in Visual Studio:" -ForegroundColor White
Write-Host "     Build > Rebuild Solution"
Write-Host "  2. Restart Visual Studio"
Write-Host "  3. Press Ctrl+Shift+P (Command Palette)"
Write-Host "  4. Type 'sync' and look for:" -ForegroundColor White
Write-Host "     'Sync Favorites ⇄ Database'" -ForegroundColor Cyan

# Alternative: Direct test
Write-Host "`n=== Alternative: Test Sync Directly ===" -ForegroundColor Cyan
Write-Host "If command doesn't appear in Command Palette, run sync directly:"
Write-Host "  cd D:\personal\dev\SearchEdgeFavorites\SearchEdgeFavorites" -ForegroundColor Yellow
Write-Host "  dotnet run" -ForegroundColor Yellow

Write-Host "`n=== Diagnostic Complete ===" -ForegroundColor Green
Write-Host ""

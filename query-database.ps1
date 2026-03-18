# SearchEdgeFavorites Database Query Tool
# Run this script to view cached favorites

$dbPath = "$env:LOCALAPPDATA\SearchEdgeFavorites\favorites_cache.db"

if (-not (Test-Path $dbPath)) {
    Write-Host "Database not found yet at: $dbPath" -ForegroundColor Yellow
    Write-Host "The database will be created after you open Edge Favorites in Command Palette." -ForegroundColor Yellow
    exit
}

Write-Host "`n=== SearchEdgeFavorites Database Tool ===" -ForegroundColor Cyan
Write-Host "Database: $dbPath`n" -ForegroundColor Gray

# Install Microsoft.Data.Sqlite if not available
if (-not (Get-Package Microsoft.Data.Sqlite -ErrorAction SilentlyContinue)) {
    Write-Host "Installing Microsoft.Data.Sqlite..." -ForegroundColor Yellow
    Install-Package Microsoft.Data.Sqlite -Force -SkipDependencies -Scope CurrentUser | Out-Null
}

# Load assembly
Add-Type -Path "$env:USERPROFILE\.nuget\packages\microsoft.data.sqlite.core\9.0.0\lib\netstandard2.0\Microsoft.Data.Sqlite.dll"

$conn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$dbPath")
$conn.Open()

# Statistics
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT 
    COUNT(*) as Total,
    SUM(CASE WHEN IsSummarized = 1 THEN 1 ELSE 0 END) as Summarized,
    SUM(CASE WHEN IsSummarized = 0 THEN 1 ELSE 0 END) as NotSummarized
FROM FavoriteCache
"@

$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    Write-Host "Statistics:" -ForegroundColor Green
    Write-Host "  Total Cached URLs: $($reader['Total'])" -ForegroundColor White
    Write-Host "  With AI Summaries: $($reader['Summarized'])" -ForegroundColor Yellow
    Write-Host "  Without Summaries: $($reader['NotSummarized'])" -ForegroundColor Red
}
$reader.Close()

# Recent summaries
Write-Host "`nRecent AI Summaries:" -ForegroundColor Green
$cmd.CommandText = @"
SELECT Title, AiDescription, LastUpdated 
FROM FavoriteCache 
WHERE IsSummarized = 1 
ORDER BY LastUpdated DESC 
LIMIT 10
"@

$reader = $cmd.ExecuteReader()
$i = 1
while ($reader.Read()) {
    Write-Host "`n$i. $($reader['Title'])" -ForegroundColor Cyan
    Write-Host "   $($reader['AiDescription'])" -ForegroundColor Gray
    Write-Host "   Updated: $($reader['LastUpdated'])" -ForegroundColor DarkGray
    $i++
}
$reader.Close()

$conn.Close()

Write-Host "`n" -NoNewline

# Database Diagnostic Script
# Shows what's actually in your SearchEdgeFavorites database

$dbPath = "$env:LOCALAPPDATA\SearchEdgeFavorites\favorites_cache.db"

Write-Host "`n=== SearchEdgeFavorites Database Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $dbPath)) {
    Write-Host "ERROR: Database not found at: $dbPath" -ForegroundColor Red
    exit
}

Write-Host "Database: $dbPath" -ForegroundColor Green
$dbSize = (Get-Item $dbPath).Length / 1KB
Write-Host "Size: $([math]::Round($dbSize, 2)) KB`n" -ForegroundColor Gray

# Load System.Data.SQLite or use a simple reader
Add-Type -AssemblyName System.Data

$connectionString = "Data Source=$dbPath;Version=3;Read Only=True;"
$connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)

try {
    $connection.Open()

    # Total records
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM FavoriteCache"
    $total = $cmd.ExecuteScalar()
    Write-Host "Total records in DB: $total" -ForegroundColor Green

    # Dead records
    $cmd.CommandText = "SELECT COUNT(*) FROM FavoriteCache WHERE IsDead = 1"
    $dead = $cmd.ExecuteScalar()
    Write-Host "Dead records (IsDead=1): $dead" -ForegroundColor $(if($dead -gt 0){"Yellow"}else{"Green"})

    # Summarized records
    $cmd.CommandText = "SELECT COUNT(*) FROM FavoriteCache WHERE IsSummarized = 1"
    $summarized = $cmd.ExecuteScalar()
    Write-Host "Summarized records: $summarized" -ForegroundColor Green

    # IsDead distribution
    Write-Host "`n=== IsDead Column Distribution ===" -ForegroundColor Cyan
    $cmd.CommandText = "SELECT IsDead, COUNT(*) as Count FROM FavoriteCache GROUP BY IsDead"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $isDeadVal = if ($reader.IsDBNull(0)) { "NULL" } else { $reader.GetInt32(0) }
        $count = $reader.GetInt32(1)
        Write-Host "  IsDead=$isDeadVal : $count records"
    }
    $reader.Close()

    # Show dead URLs if any
    if ($dead -gt 0) {
        Write-Host "`n=== Dead URLs (max 10) ===" -ForegroundColor Yellow
        $cmd.CommandText = "SELECT Url, HttpStatusCode FROM FavoriteCache WHERE IsDead = 1 LIMIT 10"
        $reader = $cmd.ExecuteReader()
        while ($reader.Read()) {
            $url = $reader.GetString(0)
            $statusCode = if ($reader.IsDBNull(1)) { "NULL" } else { $reader.GetInt32(1) }
            Write-Host "  ☠ HTTP $statusCode : $url" -ForegroundColor Red
        }
        $reader.Close()
    }

} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nTrying alternative method..." -ForegroundColor Yellow

    # Fallback: Just show file exists
    Write-Host "Database file exists but cannot query directly from PowerShell"
    Write-Host "Run the Sync command in your app - it will log detailed diagnostics"
}
finally {
    $connection.Close()
}

Write-Host "`n=== Edge Favorites Count ===" -ForegroundColor Cyan
try {
    $bookmarksPath = "$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Bookmarks"
    if (Test-Path $bookmarksPath) {
        $bookmarks = Get-Content $bookmarksPath -Raw | ConvertFrom-Json

        $favCount = 0
        function Count-Bookmarks($node) {
            if ($node.type -eq "url") { 
                $script:favCount++ 
            }
            if ($node.children) {
                foreach ($child in $node.children) {
                    Count-Bookmarks $child
                }
            }
        }

        if ($bookmarks.roots.bookmark_bar) { Count-Bookmarks $bookmarks.roots.bookmark_bar }
        if ($bookmarks.roots.other) { Count-Bookmarks $bookmarks.roots.other }
        if ($bookmarks.roots.synced) { Count-Bookmarks $bookmarks.roots.synced }

        Write-Host "Total Edge favorites: $favCount" -ForegroundColor Green

        Write-Host "`n=== Analysis ===" -ForegroundColor Cyan
        if ($total -eq $favCount) {
            Write-Host "✓ PERFECT MATCH: DB has same count as favorites" -ForegroundColor Green
        } elseif ($total -lt $favCount) {
            $missing = $favCount - $total
            Write-Host "ⓘ OK: DB has $missing fewer records (not all favorites summarized yet)" -ForegroundColor Cyan
        } else {
            $extra = $total - $favCount
            Write-Host "⚠ WARNING: DB has $extra EXTRA records (orphaned from deleted favorites)" -ForegroundColor Yellow
            Write-Host "  → Run Sync to clean up" -ForegroundColor Gray
        }

        if ($dead -gt 0) {
            Write-Host "⚠ WARNING: $dead dead URLs in DB" -ForegroundColor Yellow
            Write-Host "  → Run Sync to remove from favorites" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "Could not count Edge favorites: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""

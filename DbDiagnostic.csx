// Quick DB diagnostic tool
// Add this temporarily to see what's in your database

using SearchEdgeFavorites.Services;
using System;

Console.WriteLine("=== Database Diagnostic ===\n");

var dbService = new DatabaseService();
var cmd = dbService.CreateCommand();

if (cmd == null)
{
    Console.WriteLine("ERROR: Could not connect to database");
    return;
}

// Total records
cmd.CommandText = "SELECT COUNT(*) FROM FavoriteCache";
var total = Convert.ToInt32(cmd.ExecuteScalar());
Console.WriteLine($"Total records in DB: {total}");

// Dead records
cmd.CommandText = "SELECT COUNT(*) FROM FavoriteCache WHERE IsDead = 1";
var dead = Convert.ToInt32(cmd.ExecuteScalar());
Console.WriteLine($"Dead records (IsDead=1): {dead}");

// Summarized records
cmd.CommandText = "SELECT COUNT(*) FROM FavoriteCache WHERE IsSummarized = 1";
var summarized = Convert.ToInt32(cmd.ExecuteScalar());
Console.WriteLine($"Summarized records: {summarized}");

// Show some dead URLs
if (dead > 0)
{
    Console.WriteLine($"\n=== Dead URLs (showing max 10) ===");
    cmd.CommandText = "SELECT Url, HttpStatusCode FROM FavoriteCache WHERE IsDead = 1 LIMIT 10";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var url = reader.GetString(0);
        var statusCode = reader.IsDBNull(1) ? "NULL" : reader.GetInt32(1).ToString();
        Console.WriteLine($"  ☠ {url} (HTTP {statusCode})");
    }
}

// Check for NULL vs 0 vs 1 in IsDead column
cmd.CommandText = "SELECT IsDead, COUNT(*) FROM FavoriteCache GROUP BY IsDead";
Console.WriteLine($"\n=== IsDead column distribution ===");
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
    {
        var isDead = reader.IsDBNull(0) ? "NULL" : reader.GetInt32(0).ToString();
        var count = reader.GetInt32(1);
        Console.WriteLine($"  IsDead={isDead}: {count} records");
    }
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

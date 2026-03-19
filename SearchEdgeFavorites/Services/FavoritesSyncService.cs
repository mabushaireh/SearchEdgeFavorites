// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SearchEdgeFavorites.Models;

namespace SearchEdgeFavorites.Services;

public class FavoritesSyncService
{
    private readonly EdgeFavoritesService _edgeFavoritesService;
    private readonly DatabaseService _databaseService;
    private readonly string _bookmarksPath;

    public FavoritesSyncService(EdgeFavoritesService edgeFavoritesService, DatabaseService databaseService)
    {
        _edgeFavoritesService = edgeFavoritesService;
        _databaseService = databaseService;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _bookmarksPath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Bookmarks");
    }

    public (int RemovedFromDb, int RemovedFromFavorites, string Log) SyncFavorites()
    {
        var log = new System.Text.StringBuilder();
        var removedFromDb = 0;
        var removedFromFavorites = 0;
        var addedToDb = 0;

        try
        {
            LogToDebug("=== Starting Favorites Sync ===");

            log.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Starting Favorites Sync");
            log.AppendLine("=" + new string('=', 60));

            // Step 1: Get current favorites
            var currentFavorites = _edgeFavoritesService.GetFavorites();
            var currentUrls = new HashSet<string>(currentFavorites.Select(f => f.Url));

            log.AppendLine($"Current favorites in Edge: {currentFavorites.Count}");
            LogToDebug($"Current favorites: {currentFavorites.Count}");

            // Step 2: Get all cached URLs from DB
            var allCachedUrls = GetAllCachedUrls();
            var cachedUrlSet = new HashSet<string>(allCachedUrls);
            log.AppendLine($"Current records in database: {allCachedUrls.Count}");

            // Step 3: Get dead URLs from DB (BEFORE adding new ones)
            LogToDebug("Querying for dead URLs...");
            var deadUrls = GetDeadUrls();
            log.AppendLine();
            log.AppendLine($"--- Dead URLs in Database: {deadUrls.Count} ---");

            if (deadUrls.Count > 0)
            {
                // Log the dead URLs for verification
                foreach (var (url, statusCode) in deadUrls)
                {
                    log.AppendLine($"  ☠ Dead: {url} (HTTP {statusCode})");
                }

                // Step 4: Remove dead bookmarks from Edge AND database together
                log.AppendLine();
                log.AppendLine("--- Removing Dead Bookmarks (Edge + DB together) ---");
                removedFromFavorites = RemoveDeadBookmarksFromEdge(deadUrls, log);

                // Now delete only the URLs that were actually removed from Edge
                foreach (var url in RemovedUrls)
                {
                    if (DeleteFromDatabase(url))
                    {
                        removedFromDb++;
                        log.AppendLine($"  - Removed DB record: {url}");
                        LogToDebug($"Removed from DB (dead): {url}");
                    }
                }

                log.AppendLine($"Total removed from favorites: {removedFromFavorites}");
                log.AppendLine($"Total removed from database: {removedFromDb}");

                // Update current state after deletions
                currentFavorites = _edgeFavoritesService.GetFavorites();
                currentUrls = new HashSet<string>(currentFavorites.Select(f => f.Url));
                allCachedUrls = GetAllCachedUrls();
                cachedUrlSet = new HashSet<string>(allCachedUrls);
            }
            else
            {
                log.AppendLine("  No dead URLs to remove");
            }

            // Step 5: Add missing favorites to DB (after dead ones are removed)
            log.AppendLine();
            log.AppendLine("--- Adding Missing Favorites to Database ---");
            foreach (var favorite in currentFavorites)
            {
                if (!cachedUrlSet.Contains(favorite.Url))
                {
                    if (AddFavoriteToDatabase(favorite))
                    {
                        addedToDb++;
                        log.AppendLine($"  + Added to DB: {favorite.Name}");
                        LogToDebug($"Added to DB: {favorite.Url}");
                    }
                }
            }
            log.AppendLine($"Total added to database: {addedToDb}");

            // Step 6: Remove orphaned DB entries (shouldn't happen if sync runs regularly)
            log.AppendLine();
            log.AppendLine("--- Cleaning Orphaned Database Entries ---");
            var orphanedCount = 0;
            foreach (var cachedUrl in allCachedUrls)
            {
                if (!currentUrls.Contains(cachedUrl))
                {
                    if (DeleteFromDatabase(cachedUrl))
                    {
                        orphanedCount++;
                        log.AppendLine($"  - Removed orphaned: {cachedUrl}");
                        LogToDebug($"Removed orphaned from DB: {cachedUrl}");
                    }
                }
            }
            log.AppendLine($"Total orphaned entries removed: {orphanedCount}");

            // Final counts
            var finalFavorites = _edgeFavoritesService.GetFavorites();
            var finalDbUrls = GetAllCachedUrls();

            log.AppendLine();
            log.AppendLine("=" + new string('=', 60));
            log.AppendLine($"Sync completed successfully!");
            log.AppendLine($"");
            log.AppendLine($"Actions Taken:");
            log.AppendLine($"  - Dead links removed from Edge: {removedFromFavorites}");
            log.AppendLine($"  - Dead records removed from DB: {removedFromDb}");
            log.AppendLine($"  - New favorites added to DB: {addedToDb}");
            log.AppendLine($"  - Orphaned entries cleaned: {orphanedCount}");
            log.AppendLine();
            log.AppendLine($"Final State:");
            log.AppendLine($"  - Edge Favorites: {finalFavorites.Count}");
            log.AppendLine($"  - Database Records: {finalDbUrls.Count}");

            if (finalFavorites.Count == finalDbUrls.Count)
            {
                log.AppendLine($"  ✓ PERFECTLY SYNCED - Counts match!");
            }
            else
            {
                var diff = finalDbUrls.Count - finalFavorites.Count;
                log.AppendLine($"  ⚠ MISMATCH: DB has {Math.Abs(diff)} {(diff > 0 ? "more" : "fewer")} records");
                log.AppendLine($"    Run sync again to complete synchronization");
            }

            LogToDebug($"Sync completed: Added={addedToDb}, RemovedDB={removedFromDb}, RemovedFav={removedFromFavorites}, Orphaned={orphanedCount}");

            // Write to log file
            WriteToLogFile(log.ToString());
        }
        catch (Exception ex)
        {
            log.AppendLine();
            log.AppendLine($"ERROR: {ex.Message}");
            log.AppendLine($"Stack: {ex.StackTrace}");
            LogToDebug($"Sync error: {ex.Message}");
        }

        return (removedFromDb, removedFromFavorites, log.ToString());
    }

    private bool AddFavoriteToDatabase(Models.Favorite favorite)
    {
        try
        {
            var cmd = _databaseService.CreateCommand();
            if (cmd == null) return false;

            cmd.CommandText = @"
                INSERT INTO FavoriteCache (Url, Title, AiDescription, PageContent, LastUpdated, IsSummarized, IsDead, HttpStatusCode)
                VALUES (@url, @title, @description, @content, @updated, @summarized, @dead, @statusCode)";

            cmd.Parameters.AddWithValue("@url", favorite.Url);
            cmd.Parameters.AddWithValue("@title", favorite.Name);
            cmd.Parameters.AddWithValue("@description", string.Empty);
            cmd.Parameters.AddWithValue("@content", string.Empty);
            cmd.Parameters.AddWithValue("@updated", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@summarized", 0);
            cmd.Parameters.AddWithValue("@dead", 0);
            cmd.Parameters.AddWithValue("@statusCode", DBNull.Value);

            cmd.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            LogToDebug($"Error adding to database: {ex.Message}");
            return false;
        }
    }

    private List<string> GetAllCachedUrls()
    {
        var urls = new List<string>();
        try
        {
            var cmd = _databaseService.CreateCommand();
            if (cmd == null)
            {
                LogToDebug("GetAllCachedUrls: Database command is null");
                return urls;
            }

            cmd.CommandText = "SELECT Url FROM FavoriteCache";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                urls.Add(reader.GetString(0));
            }

            LogToDebug($"Found {urls.Count} URLs in cache");
        }
        catch (Exception ex)
        {
            LogToDebug($"Error in GetAllCachedUrls: {ex.Message}");
        }

        return urls;
    }

    private List<(string Url, int StatusCode)> GetDeadUrls()
    {
        var deadUrls = new List<(string Url, int StatusCode)>();
        try
        {
            var cmd = _databaseService.CreateCommand();
            if (cmd == null)
            {
                LogToDebug("GetDeadUrls: Database command is null");
                return deadUrls;
            }

            cmd.CommandText = "SELECT Url, HttpStatusCode FROM FavoriteCache WHERE IsDead = 1";

            LogToDebug($"Executing query: {cmd.CommandText}");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var url = reader.GetString(0);
                var statusCode = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                deadUrls.Add((url, statusCode));
                LogToDebug($"Found dead URL: {url} (HTTP {statusCode})");
            }

            LogToDebug($"Total dead URLs found: {deadUrls.Count}");
        }
        catch (Exception ex)
        {
            LogToDebug($"Error in GetDeadUrls: {ex.Message}");
        }

        return deadUrls;
    }

    private void LogToDebug(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            File.AppendAllText(logPath, 
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [SyncService] {message}\n");
        }
        catch
        {
            // Silently fail
        }
    }

    private bool DeleteFromDatabase(string url)
    {
        try
        {
            var cmd = _databaseService.CreateCommand();
            if (cmd == null) return false;

            cmd.CommandText = "DELETE FROM FavoriteCache WHERE Url = @url";
            cmd.Parameters.AddWithValue("@url", url);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private int RemoveDeadBookmarksFromEdge(List<(string Url, int StatusCode)> deadUrls, System.Text.StringBuilder log)
    {
        var removedCount = 0;
        RemovedUrls = new HashSet<string>(); // Track successfully removed URLs

        try
        {
            if (!File.Exists(_bookmarksPath))
            {
                log.AppendLine("  ⚠ Bookmarks file not found");
                return 0;
            }

            // Create backup first
            var backupPath = _bookmarksPath + $".backup.{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(_bookmarksPath, backupPath, true);
            log.AppendLine($"  ✓ Backup created: {Path.GetFileName(backupPath)}");

            // Read bookmarks
            var json = File.ReadAllText(_bookmarksPath);
            var bookmarkRoot = JsonSerializer.Deserialize<BookmarkRoot>(json);

            if (bookmarkRoot?.Roots == null)
            {
                log.AppendLine("  ⚠ Could not parse bookmarks file");
                return 0;
            }

            var deadUrlSet = new HashSet<string>(deadUrls.Select(d => d.Url));

            // Remove dead bookmarks from each section
            if (bookmarkRoot.Roots.BookmarkBar != null)
            {
                removedCount += RemoveDeadFromFolder(bookmarkRoot.Roots.BookmarkBar, deadUrlSet, log);
            }

            if (bookmarkRoot.Roots.Other != null)
            {
                removedCount += RemoveDeadFromFolder(bookmarkRoot.Roots.Other, deadUrlSet, log);
            }

            if (bookmarkRoot.Roots.Synced != null)
            {
                removedCount += RemoveDeadFromFolder(bookmarkRoot.Roots.Synced, deadUrlSet, log);
            }

            // Write updated bookmarks back
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var updatedJson = JsonSerializer.Serialize(bookmarkRoot, options);
            File.WriteAllText(_bookmarksPath, updatedJson);

            log.AppendLine($"  ✓ Bookmarks file updated");
        }
        catch (Exception ex)
        {
            log.AppendLine($"  ✗ Error removing dead bookmarks: {ex.Message}");
        }

        return removedCount;
    }

    private HashSet<string> RemovedUrls { get; set; } = new HashSet<string>(); // Track removed URLs

    private int RemoveDeadFromFolder(BookmarkFolder folder, HashSet<string> deadUrls, System.Text.StringBuilder log)
    {
        var removedCount = 0;

        if (folder.Children == null) return 0;

        var childrenToRemove = new List<BookmarkNode>();

        foreach (var child in folder.Children)
        {
            if (child.Type == "url" && !string.IsNullOrEmpty(child.Url))
            {
                if (deadUrls.Contains(child.Url))
                {
                    childrenToRemove.Add(child);
                    log.AppendLine($"  ☠ Removing: {child.Name}");
                    removedCount++;
                }
            }
            else if (child.Type == "folder" && child.Children != null)
            {
                // Recursively process subfolders
                removedCount += RemoveDeadFromNode(child, deadUrls, log);
            }
        }

        // Remove marked children
        foreach (var child in childrenToRemove)
        {
            folder.Children.Remove(child);
        }

        return removedCount;
    }

    private int RemoveDeadFromNode(BookmarkNode node, HashSet<string> deadUrls, System.Text.StringBuilder log)
    {
        var removedCount = 0;

        if (node.Children == null) return 0;

        var childrenToRemove = new List<BookmarkNode>();

        foreach (var child in node.Children)
        {
            if (child.Type == "url" && !string.IsNullOrEmpty(child.Url))
            {
                if (deadUrls.Contains(child.Url))
                {
                    childrenToRemove.Add(child);
                    log.AppendLine($"  ☠ Removing: {child.Name}");
                    removedCount++;
                }
            }
            else if (child.Type == "folder" && child.Children != null)
            {
                // Recursively process subfolders
                removedCount += RemoveDeadFromNode(child, deadUrls, log);
            }
        }

        // Remove marked children
        foreach (var child in childrenToRemove)
        {
            node.Children.Remove(child);
        }

        return removedCount;
    }

    private void WriteToLogFile(string content)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "sync.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, content + "\n\n");
        }
        catch
        {
            // Silently fail if logging fails
        }
    }
}

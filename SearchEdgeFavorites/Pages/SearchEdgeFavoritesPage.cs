// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SearchEdgeFavorites.Services;
using SearchEdgeFavorites.Helpers;
using Windows.Foundation;
using Windows.System;

namespace SearchEdgeFavorites;

internal sealed partial class SearchEdgeFavoritesPage : ListPage
{
    private readonly EdgeFavoritesService _favoritesService;
    private readonly DatabaseService _databaseService;
    private readonly CacheUpdateService _cacheUpdateService;
    private readonly FaviconService _faviconService;

    public SearchEdgeFavoritesPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Edge Favorites AI ??";
        Name = "Open";
        PlaceholderText = "?? Search your favorites with AI descriptions...";

        _favoritesService = new EdgeFavoritesService();
        _databaseService = new DatabaseService();
        _faviconService = new FaviconService();

        var webScraperService = new WebScraperService();
        var aiSummaryService = new UnifiedAiService();
        _cacheUpdateService = new CacheUpdateService(_databaseService, webScraperService, aiSummaryService);
    }

    public override IListItem[] GetItems()
    {
        var favorites = _favoritesService.GetFavorites();
        var items = new List<IListItem>();

        // Always add Sync Favorites option at the top
        items.Add(new ListItem(new SyncFavoritesCommand())
        {
            Title = "Sync Favorites",
            Subtitle = "Clean DB of deleted favorites & remove dead bookmarks from Edge",
            Icon = new IconInfo("\uE895") // Sync icon
        });
        // Add consistency check option
        items.Add(new ListItem(new CheckConsistencyCommand())
        {
            Title = "Check Consistency",
            Subtitle = "View detailed DB vs Edge sync status & find issues",
            Icon = new IconInfo("\uE9D9") // CheckList/Diagnostic icon
        });
        if (!favorites.Any())
        {
            items.Add(new ListItem(new NoOpCommand()) 
            { 
                Title = "No favorites found",
                Subtitle = "Make sure Microsoft Edge has bookmarks saved" 
            });
            return items.ToArray();
        }

        // Queue uncached URLs for background processing
        _cacheUpdateService.QueueUrlsForProcessing(favorites);

        // Add all favorite items
        foreach (var fav in favorites)
        {
            // Check if we have a cached description
            var cached = _databaseService.GetCachedFavorite(fav.Url);

            var subtitle = fav.Url;
            if (cached != null && !string.IsNullOrEmpty(cached.AiDescription))
            {
                subtitle = $"?? {cached.AiDescription}";
            }

            // Try to get favicon
            var faviconPath = _faviconService.GetCachedFaviconPath(fav.Url);
            if (faviconPath == null)
            {
                // Queue download for next time
                _faviconService.QueueFaviconDownload(fav.Url);
            }

            var item = new ListItem(new OpenUrlCommand(fav.Url))
            {
                Title = fav.Name,
                Subtitle = subtitle,
                Icon = DomainIconHelper.GetIconForUrl(fav.Url)
            };

            items.Add(item);
        }

        return items.ToArray();
    }
}

internal class OpenUrlCommand : InvokableCommand
{
    private readonly string _url;

    public OpenUrlCommand(string url)
    {
        _url = url;
        Id = _url;
        Name = "Open URL";
    }

    public override ICommandResult Invoke()
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "SearchEdgeFavorites", "url_open.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"\n{DateTime.Now:HH:mm:ss} - Opening: {_url}\n");

            // Method 1: cmd.exe start (most reliable)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{_url}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                File.AppendAllText(logPath, $"  ? Opened via cmd.exe\n");
                return CommandResult.KeepOpen();
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"  ? cmd.exe failed: {ex.Message}\n");
            }

            // Method 2: Direct shell execute
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = _url,
                    UseShellExecute = true
                });
                File.AppendAllText(logPath, $"  ? Opened via ShellExecute\n");
                return CommandResult.KeepOpen();
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"  ? ShellExecute failed: {ex.Message}\n");
            }

            File.AppendAllText(logPath, $"  ? ALL METHODS FAILED\n");
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(logPath, $"  CRITICAL: {ex.Message}\n"); } catch { }
        }

        return CommandResult.KeepOpen();
    }
}

internal class SyncFavoritesCommand : InvokableCommand
{
    public SyncFavoritesCommand()
    {
        Id = "sync-favorites";
        Name = "Sync Favorites";
    }

    public override ICommandResult Invoke()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites",
            "sync_result.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            File.AppendAllText(logPath, $"\n{'=',-80}\n");
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Starting Sync...\n");
            File.AppendAllText(logPath, $"{'=',-80}\n");

            var edgeService = new EdgeFavoritesService();
            var dbService = new DatabaseService();
            var syncService = new FavoritesSyncService(edgeService, dbService);

            var (removedFromDb, removedFromFavorites, log) = syncService.SyncFavorites();

            // Write result to log
            var result = $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Sync completed!\n" +
                        $"• Removed from database: {removedFromDb}\n" +
                        $"• Removed from favorites: {removedFromFavorites}\n\n";

            File.AppendAllText(logPath, result);
            File.AppendAllText(logPath, "Check sync.log for full details.\n");

            // Open the log file
            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });

            return CommandResult.KeepOpen();
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Sync failed: {ex.Message}\n");
            File.AppendAllText(logPath, $"Stack: {ex.StackTrace}\n");

            // Still try to open the log
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            catch { }

            return CommandResult.KeepOpen();
        }
    }
}

internal class CheckConsistencyCommand : InvokableCommand
{
    public CheckConsistencyCommand()
    {
        Id = "check-consistency";
        Name = "Check Consistency";
    }

    public override ICommandResult Invoke()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites",
            "consistency_report.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var edgeService = new EdgeFavoritesService();
            var dbService = new DatabaseService();
            var syncService = new FavoritesSyncService(edgeService, dbService);

            // Run consistency check
            var report = syncService.CheckConsistency();

            // Write report to file
            File.WriteAllText(logPath, report);

            // Open the report file
            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });

            // Also write to sync_result.log for easy access
            var resultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "sync_result.log");

            File.AppendAllText(resultPath, $"\n{'=',-80}\n");
            File.AppendAllText(resultPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Consistency Check Completed\n");
            File.AppendAllText(resultPath, $"{'=',-80}\n");
            File.AppendAllText(resultPath, "Full report saved to: consistency_report.log\n\n");

            return CommandResult.KeepOpen();
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(logPath, 
                    $"? ERROR during consistency check:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");

                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            catch { }

            return CommandResult.KeepOpen();
        }
    }
}

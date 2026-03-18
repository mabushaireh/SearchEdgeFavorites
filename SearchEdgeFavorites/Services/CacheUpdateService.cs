// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SearchEdgeFavorites.Models;

namespace SearchEdgeFavorites.Services;

public class CacheUpdateService
{
    private readonly DatabaseService _databaseService;
    private readonly WebScraperService _webScraperService;
    private readonly IAiSummaryService _aiSummaryService;
    private bool _isProcessing = false;

    public CacheUpdateService(
        DatabaseService databaseService,
        WebScraperService webScraperService,
        IAiSummaryService aiSummaryService)
    {
        _databaseService = databaseService;
        _webScraperService = webScraperService;
        _aiSummaryService = aiSummaryService;
    }

    public void QueueUrlsForProcessing(List<Favorite> favorites)
    {
        if (_isProcessing || !_aiSummaryService.IsConfigured())
        {
            return;
        }

        // Start background processing without blocking
        Task.Run(async () => await ProcessUrlsAsync(favorites));
    }

    private async Task ProcessUrlsAsync(List<Favorite> favorites)
    {
        _isProcessing = true;

        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            File.AppendAllText(logPath, 
                $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Starting cache update for {favorites.Count} favorites\n");
            File.AppendAllText(logPath, 
                $"  Note: Will attempt to generate 5 AI summaries per session (rate limiting)\n");

            // Process favorites until we get 5 successful AI summarizations
            var successfulAiCalls = 0;
            var processedCount = 0;
            const int targetSummaries = 5;
            const int maxAttempts = 400; // Increased limit to handle many corporate URLs

            foreach (var favorite in favorites)
            {
                // Stop if we've reached our target or max attempts
                if (successfulAiCalls >= targetSummaries || processedCount >= maxAttempts)
                {
                    break;
                }

                processedCount++;

                try
                {
                    var cached = _databaseService.GetCachedFavorite(favorite.Url);

                    // Skip if already summarized within last 7 days
                    if (cached != null && cached.IsSummarized && 
                        (DateTime.Now - cached.LastUpdated).TotalDays < 7)
                    {
                        continue;
                    }

                    File.AppendAllText(logPath, 
                        $"  [{processedCount}] Processing: {favorite.Url}\n");

                    // Fetch page content
                    var (title, content) = await _webScraperService.FetchPageContentAsync(favorite.Url);

                    if (string.IsNullOrEmpty(content))
                    {
                        File.AppendAllText(logPath, 
                            $"      Failed to fetch content (may require authentication) - trying next URL\n");

                        // Cache the favorite even without content so we don't keep retrying
                        var emptyCache = new FavoriteCache
                        {
                            Url = favorite.Url,
                            Title = favorite.Name,
                            AiDescription = string.Empty,
                            PageContent = string.Empty,
                            LastUpdated = DateTime.Now,
                            IsSummarized = false
                        };
                        _databaseService.UpsertCache(emptyCache);
                        continue; // Don't count toward AI call limit
                    }

                    File.AppendAllText(logPath, 
                        $"      Fetched content, generating AI summary...\n");

                    // Generate AI summary
                    var summary = await _aiSummaryService.GenerateSummaryAsync(title, content, favorite.Url);

                    // Save to database
                    var cache = new FavoriteCache
                    {
                        Url = favorite.Url,
                        Title = !string.IsNullOrEmpty(title) ? title : favorite.Name,
                        AiDescription = summary,
                        PageContent = content.Length > 1000 ? content.Substring(0, 1000) : content,
                        LastUpdated = DateTime.Now,
                        IsSummarized = !string.IsNullOrEmpty(summary)
                    };

                    _databaseService.UpsertCache(cache);

                    if (!string.IsNullOrEmpty(summary))
                    {
                        successfulAiCalls++; // Only count successful AI summarizations
                        File.AppendAllText(logPath, 
                            $"      ✓ AI Summary #{successfulAiCalls}: {summary.Substring(0, Math.Min(50, summary.Length))}...\n");
                    }
                    else
                    {
                        File.AppendAllText(logPath, 
                            $"      ✗ AI summarization failed - trying next URL\n");
                        continue; // Don't count toward AI call limit
                    }

                    // Delay to respect rate limits
                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logPath, 
                        $"    Error processing {favorite.Url}: {ex.Message}\n");
                }
            }

            var totalCached = _databaseService.GetUnsummarizedUrls(1000).Count;
            var remaining = favorites.Count - totalCached;
            File.AppendAllText(logPath, 
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Cache update completed\n");
            File.AppendAllText(logPath, 
                $"  Processed {processedCount} URLs, generated {successfulAiCalls} AI summaries\n");
            File.AppendAllText(logPath, 
                $"  Total cached so far: {totalCached}/{favorites.Count} ({remaining} remaining)\n");
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SearchEdgeFavorites",
                    "debug.log");
                File.AppendAllText(logPath, 
                    $"CRITICAL ERROR in cache update: {ex.Message}\n");
            }
            catch
            {
                // Can't log
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }
}

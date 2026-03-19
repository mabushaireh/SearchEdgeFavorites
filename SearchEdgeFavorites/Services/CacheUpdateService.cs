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

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var targetSummaries = ConfigurationService.Instance.MaxAiSummariesPerSession;
            var maxAttempts = ConfigurationService.Instance.MaxScrapingAttempts;

            File.AppendAllText(logPath, 
                $"\n{'=',-80}\n");
            File.AppendAllText(logPath, 
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - SESSION START: Cache Update\n");
            File.AppendAllText(logPath, 
                $"{'=',-80}\n");
            File.AppendAllText(logPath, 
                $"Configuration:\n");
            File.AppendAllText(logPath, 
                $"  - Total favorites to process: {favorites.Count}\n");
            File.AppendAllText(logPath, 
                $"  - Target AI summaries per session: {targetSummaries}\n");
            File.AppendAllText(logPath, 
                $"  - Max scraping attempts: {maxAttempts}\n");
            File.AppendAllText(logPath, 
                $"  - HTTP timeout: {ConfigurationService.Instance.HttpTimeoutSeconds}s\n");
            File.AppendAllText(logPath, 
                $"  - Delay between requests: {ConfigurationService.Instance.DelayBetweenRequestsMs}ms\n");
            File.AppendAllText(logPath, 
                $"  - Cache expiry: {ConfigurationService.Instance.CacheExpiryDays} days\n");
            File.AppendAllText(logPath, 
                $"  - AI Provider: {ConfigurationService.Instance.GetConfigValue("AI_PROVIDER", "openai")}\n");
            File.AppendAllText(logPath, 
                $"\n");

            // Process favorites until we get target successful AI summarizations
            var successfulAiCalls = 0;
            var attemptedFetches = 0;
            var skippedCount = 0;

            foreach (var favorite in favorites)
            {
                // Stop if we've reached our target or max attempts
                if (successfulAiCalls >= targetSummaries || attemptedFetches >= maxAttempts)
                {
                    break;
                }

                try
                {
                    var cached = _databaseService.GetCachedFavorite(favorite.Url);

                    // Skip if already marked as dead
                    if (cached != null && cached.IsDead)
                    {
                        skippedCount++;
                        File.AppendAllText(logPath, 
                            $"  [Skipped] Dead page (HTTP {cached.HttpStatusCode}): {favorite.Url}\n");
                        continue;
                    }

                    // Skip if already summarized within configured expiry period
                    var cacheExpiryDays = ConfigurationService.Instance.CacheExpiryDays;
                    if (cached != null && cached.IsSummarized && 
                        (DateTime.Now - cached.LastUpdated).TotalDays < cacheExpiryDays)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Now we're actually going to attempt a fetch - increment counter
                    attemptedFetches++;

                    File.AppendAllText(logPath, 
                        $"  [Attempt #{attemptedFetches}] Processing: {favorite.Url}\n");

                    // Fetch page content
                    var (title, content, statusCode) = await _webScraperService.FetchPageContentAsync(favorite.Url);

                    // Handle 404 Not Found - mark as dead
                    if (statusCode == 404)
                    {
                        File.AppendAllText(logPath, 
                            $"      ☠ Page not found (404) - marking as DEAD and will skip in future sessions\n");

                        var deadCache = new FavoriteCache
                        {
                            Url = favorite.Url,
                            Title = favorite.Name,
                            AiDescription = "Page not found (404)",
                            PageContent = string.Empty,
                            LastUpdated = DateTime.Now,
                            IsSummarized = false,
                            IsDead = true,
                            HttpStatusCode = 404
                        };
                        _databaseService.UpsertCache(deadCache);
                        continue; // Don't count toward AI call limit
                    }

                    // Handle other HTTP errors (401, 403, 500, etc.)
                    if (statusCode.HasValue && (statusCode < 200 || statusCode >= 300))
                    {
                        File.AppendAllText(logPath, 
                            $"      HTTP Error {statusCode} - caching without summary\n");

                        var errorCache = new FavoriteCache
                        {
                            Url = favorite.Url,
                            Title = favorite.Name,
                            AiDescription = $"HTTP Error {statusCode}",
                            PageContent = string.Empty,
                            LastUpdated = DateTime.Now,
                            IsSummarized = false,
                            IsDead = false, // Don't mark as dead - might be temporary
                            HttpStatusCode = statusCode
                        };
                        _databaseService.UpsertCache(errorCache);
                        continue; // Don't count toward AI call limit
                    }

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
                            IsSummarized = false,
                            IsDead = false,
                            HttpStatusCode = statusCode
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
                        IsSummarized = !string.IsNullOrEmpty(summary),
                        IsDead = false,
                        HttpStatusCode = statusCode
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
                    var delayMs = ConfigurationService.Instance.DelayBetweenRequestsMs;
                    await Task.Delay(delayMs);
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
                $"\n{'=',-80}\n");
            File.AppendAllText(logPath, 
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - SESSION END: Cache Update Completed\n");
            File.AppendAllText(logPath, 
                $"{'=',-80}\n");
            File.AppendAllText(logPath, 
                $"Summary:\n");
            File.AppendAllText(logPath, 
                $"  - URLs attempted: {attemptedFetches}\n");
            File.AppendAllText(logPath, 
                $"  - URLs skipped (cached/dead): {skippedCount}\n");
            File.AppendAllText(logPath, 
                $"  - AI summaries generated: {successfulAiCalls}\n");
            File.AppendAllText(logPath, 
                $"  - Total cached: {totalCached}/{favorites.Count}\n");
            File.AppendAllText(logPath, 
                $"  - Remaining unsummarized: {remaining}\n");
            File.AppendAllText(logPath, 
                $"{'=',-80}\n\n");
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

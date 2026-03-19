// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace SearchEdgeFavorites.Services;

public class ConfigurationService
{
    private static ConfigurationService? _instance;
    private readonly string _configPath;
    private readonly HashSet<string> _overriddenSettings = new();

    public static ConfigurationService Instance => _instance ??= new ConfigurationService();

    // AI Summary Limits
    public int MaxAiSummariesPerSession { get; private set; } = 5;
    public int MaxRateLimitRetries { get; private set; } = 5;
    public int DelayBetweenRequestsMs { get; private set; } = 2000;

    // Web Scraping Limits
    public int MaxScrapingAttempts { get; private set; } = 100;
    public int HttpTimeoutSeconds { get; private set; } = 10;
    public int MaxParagraphs { get; private set; } = 20;
    public int MaxContentCharacters { get; private set; } = 4000;

    // Cache Settings
    public int CacheExpiryDays { get; private set; } = 7;

    private readonly Dictionary<string, string> _rawConfig = new();

    private ConfigurationService()
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites",
            "config.txt");

        LoadConfiguration();
    }

    public string GetConfigValue(string key, string defaultValue = "")
    {
        return _rawConfig.TryGetValue(key.ToUpperInvariant(), out var value) ? value : defaultValue;
    }

    private void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return;
            }

            var lines = File.ReadAllLines(_configPath);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                var parts = trimmedLine.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim().ToUpperInvariant();
                var value = parts[1].Trim();

                // Store raw config for logging
                _rawConfig[key] = value;

                switch (key)
                {
                    case "MAX_AI_SUMMARIES_PER_SESSION":
                        if (int.TryParse(value, out var maxSummaries) && maxSummaries > 0)
                        {
                            MaxAiSummariesPerSession = maxSummaries;
                            _overriddenSettings.Add("MaxAiSummariesPerSession");
                        }
                        break;

                    case "MAX_RATE_LIMIT_RETRIES":
                        if (int.TryParse(value, out var maxRetries) && maxRetries > 0)
                        {
                            MaxRateLimitRetries = maxRetries;
                            _overriddenSettings.Add("MaxRateLimitRetries");
                        }
                        break;

                    case "DELAY_BETWEEN_REQUESTS_MS":
                        if (int.TryParse(value, out var delay) && delay >= 0)
                        {
                            DelayBetweenRequestsMs = delay;
                            _overriddenSettings.Add("DelayBetweenRequestsMs");
                        }
                        break;

                    case "MAX_SCRAPING_ATTEMPTS":
                        if (int.TryParse(value, out var maxAttempts) && maxAttempts > 0)
                        {
                            MaxScrapingAttempts = maxAttempts;
                            _overriddenSettings.Add("MaxScrapingAttempts");
                        }
                        break;

                    case "HTTP_TIMEOUT_SECONDS":
                        if (int.TryParse(value, out var timeout) && timeout > 0)
                        {
                            HttpTimeoutSeconds = timeout;
                            _overriddenSettings.Add("HttpTimeoutSeconds");
                        }
                        break;

                    case "MAX_PARAGRAPHS":
                        if (int.TryParse(value, out var maxPara) && maxPara > 0)
                        {
                            MaxParagraphs = maxPara;
                            _overriddenSettings.Add("MaxParagraphs");
                        }
                        break;

                    case "MAX_CONTENT_CHARACTERS":
                        if (int.TryParse(value, out var maxChars) && maxChars > 0)
                        {
                            MaxContentCharacters = maxChars;
                            _overriddenSettings.Add("MaxContentCharacters");
                        }
                        break;

                    case "CACHE_EXPIRY_DAYS":
                        if (int.TryParse(value, out var expiryDays) && expiryDays > 0)
                        {
                            CacheExpiryDays = expiryDays;
                            _overriddenSettings.Add("CacheExpiryDays");
                        }
                        break;
                }
            }
        }
        catch
        {
            // Failed to load config, use defaults
        }
    }

    public void LogConfiguration()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            File.AppendAllText(logPath,
                $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} === CONFIGURATION LOADED ===\n");

            File.AppendAllText(logPath,
                $"  Max AI Summaries Per Session: {MaxAiSummariesPerSession} {GetSource("MaxAiSummariesPerSession")}\n");
            File.AppendAllText(logPath,
                $"  Max Rate Limit Retries: {MaxRateLimitRetries} {GetSource("MaxRateLimitRetries")}\n");
            File.AppendAllText(logPath,
                $"  Delay Between Requests: {DelayBetweenRequestsMs}ms {GetSource("DelayBetweenRequestsMs")}\n");
            File.AppendAllText(logPath,
                $"  Max Scraping Attempts: {MaxScrapingAttempts} {GetSource("MaxScrapingAttempts")}\n");
            File.AppendAllText(logPath,
                $"  HTTP Timeout: {HttpTimeoutSeconds}s {GetSource("HttpTimeoutSeconds")}\n");
            File.AppendAllText(logPath,
                $"  Max Paragraphs: {MaxParagraphs} {GetSource("MaxParagraphs")}\n");
            File.AppendAllText(logPath,
                $"  Max Content Characters: {MaxContentCharacters} {GetSource("MaxContentCharacters")}\n");
            File.AppendAllText(logPath,
                $"  Cache Expiry: {CacheExpiryDays} days {GetSource("CacheExpiryDays")}\n");

            File.AppendAllText(logPath, "================================\n");
        }
        catch
        {
            // Can't log
        }
    }

    private string GetSource(string settingName)
    {
        return _overriddenSettings.Contains(settingName) ? "(from config)" : "(default)";
    }
}

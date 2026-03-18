// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SearchEdgeFavorites.Services;

public class UnifiedAiService : IAiSummaryService
{
    private readonly List<IAiSummaryService> _providers;
    private readonly string _preferredProvider;

    public string ProviderName => "Unified AI Service";

    public UnifiedAiService()
    {
        _providers = new List<IAiSummaryService>
        {
            new OpenAiSummaryService(),
            new GeminiSummaryService()
        };

        _preferredProvider = LoadPreferredProvider();

        // Log configuration for debugging
        LogConfiguration();
    }

    private string LoadPreferredProvider()
    {
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "config.txt");

            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("AI_PROVIDER="))
                    {
                        return line.Substring("AI_PROVIDER=".Length).Trim().ToLower();
                    }
                }
            }
        }
        catch
        {
            // Failed to load config
        }

        return "openai"; // Default to OpenAI
    }

    public async Task<string> GenerateSummaryAsync(string title, string content, string url)
    {
        var configuredProviders = _providers.Where(p => p.IsConfigured()).ToList();

        if (!configuredProviders.Any())
        {
            LogMessage("No AI providers configured");
            return string.Empty;
        }

        // Try preferred provider first
        if (_preferredProvider == "gemini")
        {
            var gemini = configuredProviders.FirstOrDefault(p => p.ProviderName.Contains("Gemini"));
            if (gemini != null)
            {
                var result = await TryGenerateSummary(gemini, title, content, url);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
        }
        else if (_preferredProvider == "openai")
        {
            var openai = configuredProviders.FirstOrDefault(p => p.ProviderName.Contains("OpenAI"));
            if (openai != null)
            {
                var result = await TryGenerateSummary(openai, title, content, url);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
        }
        else if (_preferredProvider == "both")
        {
            // Try all configured providers in parallel and return first successful result
            var tasks = configuredProviders.Select(p => TryGenerateSummary(p, title, content, url)).ToList();
            var results = await Task.WhenAll(tasks);
            var firstSuccess = results.FirstOrDefault(r => !string.IsNullOrEmpty(r));
            if (!string.IsNullOrEmpty(firstSuccess))
            {
                return firstSuccess;
            }
        }

        // Fallback: try any remaining configured provider
        foreach (var provider in configuredProviders)
        {
            var result = await TryGenerateSummary(provider, title, content, url);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return string.Empty;
    }

    private async Task<string> TryGenerateSummary(IAiSummaryService provider, string title, string content, string url)
    {
        try
        {
            LogMessage($"Attempting summary with {provider.ProviderName} for {url}");
            var result = await provider.GenerateSummaryAsync(title, content, url);

            if (!string.IsNullOrEmpty(result))
            {
                LogMessage($"✓ Success with {provider.ProviderName}");
                return result;
            }
            else
            {
                LogMessage($"  {provider.ProviderName} returned empty result");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"✗ {provider.ProviderName} exception: {ex.Message}");
        }

        return string.Empty;
    }

    public bool IsConfigured()
    {
        return _providers.Any(p => p.IsConfigured());
    }

    private void LogConfiguration()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} === AI SERVICE CONFIGURATION ===");
            sb.AppendLine($"  Preferred Provider: {_preferredProvider}");
            sb.AppendLine($"  Available Providers:");

            foreach (var provider in _providers)
            {
                var configured = provider.IsConfigured() ? "✓ Configured" : "✗ Not Configured";
                sb.AppendLine($"    - {provider.ProviderName}: {configured}");
            }

            var configuredCount = _providers.Count(p => p.IsConfigured());
            sb.AppendLine($"  Total Configured: {configuredCount}/{_providers.Count}");
            sb.AppendLine($"==================================\n");

            File.AppendAllText(logPath, sb.ToString());
        }
        catch
        {
            // Can't log
        }
    }

    private void LogMessage(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            File.AppendAllText(logPath, 
                $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
        catch
        {
            // Can't even log
        }
    }
}

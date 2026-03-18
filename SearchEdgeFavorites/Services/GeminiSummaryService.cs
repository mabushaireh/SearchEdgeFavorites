// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SearchEdgeFavorites.Services;

public class GeminiSummaryService : IAiSummaryService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    // Using gemini-2.0-flash - confirmed available via ListModels API
    private const string ApiEndpoint = "https://generativelanguage.googleapis.com/v1/models/gemini-2.0-flash:generateContent";

    public string ProviderName => "Google Gemini";

    public GeminiSummaryService()
    {
        _apiKey = LoadApiKey();
        _httpClient = new HttpClient();

        // Log configuration status
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites",
            "debug.log");

        try
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your_gemini_api_key_here")
            {
                File.AppendAllText(logPath, 
                    $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [Gemini] API key not configured\n");
            }
            else
            {
                var maskedKey = _apiKey.Substring(0, Math.Min(10, _apiKey.Length)) + "...";
                File.AppendAllText(logPath, 
                    $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [Gemini] API key loaded: {maskedKey}\n");
            }
        }
        catch { }
    }

    private string LoadApiKey()
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
                    if (line.StartsWith("GEMINI_API_KEY="))
                    {
                        return line.Substring("GEMINI_API_KEY=".Length).Trim();
                    }
                }
            }
        }
        catch
        {
            // Failed to load config
        }

        return string.Empty;
    }

    public async Task<string> GenerateSummaryAsync(string title, string content, string url)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your_gemini_api_key_here" || string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        const int maxRateLimitRetries = 5;
        var rateLimitRetries = 0;

        while (rateLimitRetries < maxRateLimitRetries)
        {
            try
            {
                var prompt = $@"Summarize this webpage in 1-2 concise sentences that describe what the page is about and why someone might visit it.

Title: {title}
URL: {url}
Content:
{content}

Summary:";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{ApiEndpoint}?key={_apiKey}", requestContent);
                var responseBody = await response.Content.ReadAsStringAsync();

                // Handle rate limit errors with retry
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    rateLimitRetries++;

                    // Try to extract retry delay from response
                    var retryDelaySeconds = ExtractRetryDelay(responseBody);

                    if (retryDelaySeconds > 0 && rateLimitRetries < maxRateLimitRetries)
                    {
                        LogError(url, $"Rate limit hit (attempt {rateLimitRetries}/{maxRateLimitRetries}). Waiting {retryDelaySeconds}s before retry...");
                        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                        continue; // Retry
                    }
                    else
                    {
                        LogError(url, $"Rate limit exceeded after {rateLimitRetries} retries: {responseBody}");
                        return string.Empty;
                    }
                }

                // Handle other errors
                if (!response.IsSuccessStatusCode)
                {
                    LogError(url, $"API Error {response.StatusCode}: {responseBody}");
                    return string.Empty;
                }

                // Parse successful response
                var responseJson = JsonDocument.Parse(responseBody);
                var text = responseJson.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogError(url, ex.Message);
                return string.Empty;
            }
        }

        LogError(url, $"Failed after {maxRateLimitRetries} rate limit retries");
        return string.Empty;
    }

    private int ExtractRetryDelay(string errorResponse)
    {
        try
        {
            var json = JsonDocument.Parse(errorResponse);

            // Try to get retryDelay from error details
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("details", out var details))
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.TryGetProperty("@type", out var type) && 
                            type.GetString()?.Contains("RetryInfo") == true)
                        {
                            if (detail.TryGetProperty("retryDelay", out var retryDelay))
                            {
                                var delayStr = retryDelay.GetString();
                                if (!string.IsNullOrEmpty(delayStr))
                                {
                                    // Parse formats like "22s" or "22.169421824s"
                                    delayStr = delayStr.TrimEnd('s');
                                    if (double.TryParse(delayStr, out var seconds))
                                    {
                                        return (int)Math.Ceiling(seconds); // Round up to be safe
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Failed to parse, use default delay
        }

        return 30; // Default retry delay if we can't parse the response
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey) && _apiKey != "your_gemini_api_key_here";
    }

    private void LogError(string url, string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            File.AppendAllText(logPath, 
                $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Gemini Error for {url}: {message}\n");
        }
        catch
        {
            // Can't even log
        }
    }
}

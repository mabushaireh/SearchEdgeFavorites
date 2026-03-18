// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace SearchEdgeFavorites.Services;

public class OpenAiSummaryService : IAiSummaryService
{
    private readonly string _apiKey;
    private ChatClient? _chatClient;

    public string ProviderName => "OpenAI GPT-3.5";

    public OpenAiSummaryService()
    {
        _apiKey = LoadApiKey();

        // Log configuration status
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites",
            "debug.log");

        try
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your_api_key_here")
            {
                File.AppendAllText(logPath, 
                    $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [OpenAI] API key not configured\n");
            }
            else
            {
                var maskedKey = _apiKey.Substring(0, Math.Min(10, _apiKey.Length)) + "...";
                File.AppendAllText(logPath, 
                    $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [OpenAI] API key loaded: {maskedKey}\n");
            }
        }
        catch { }

        if (!string.IsNullOrEmpty(_apiKey))
        {
            try
            {
                _chatClient = new ChatClient("gpt-3.5-turbo", _apiKey);
                File.AppendAllText(logPath, 
                    $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [OpenAI] ChatClient initialized successfully\n");
            }
            catch (Exception ex)
            {
                // Client creation failed - API key might be invalid
                File.AppendAllText(logPath, 
                    $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [OpenAI] ChatClient initialization FAILED: {ex.GetType().Name} - {ex.Message}\n");
            }
        }
        else
        {
            File.AppendAllText(logPath, 
                $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [OpenAI] API key is empty, ChatClient not created\n");
        }
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
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("OPENAI_API_KEY=", StringComparison.OrdinalIgnoreCase))
                    {
                        var keyStartIndex = trimmedLine.IndexOf('=') + 1;
                        return trimmedLine.Substring(keyStartIndex).Trim();
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
        if (_chatClient == null || string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        try
        {
            var prompt = $@"Generate a compressed summary using key phrases and technical terms. Focus on main topics, technologies, concepts, and features. Keep it 30-40 words. Use keywords that someone would search for. Avoid filler like 'this webpage provides', 'learn how to', 'information about'.

Title: {title}
URL: {url}
Content:
{content}

Compressed Summary:";

            var chatCompletion = await _chatClient.CompleteChatAsync(prompt);

            return chatCompletion.Value.Content[0].Text.Trim();
        }
        catch (Exception ex)
        {
            LogError(url, ex.Message);
            return string.Empty;
        }
    }

    public bool IsConfigured()
    {
        return _chatClient != null && !string.IsNullOrEmpty(_apiKey) && _apiKey != "your_api_key_here";
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
                $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - OpenAI Error for {url}: {message}\n");
        }
        catch
        {
            // Can't even log
        }
    }
}

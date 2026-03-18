// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace SearchEdgeFavorites.Services;

public class WebScraperService
{
    private readonly HttpClient _httpClient;

    public WebScraperService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(ConfigurationService.Instance.HttpTimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<(string Title, string Content)> FetchPageContentAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            return ParseHtml(html);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private (string Title, string Content) ParseHtml(string html)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract title
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty;

            // Extract main content - prioritize article, main, or body
            var contentNodes = doc.DocumentNode.SelectNodes("//article | //main | //body//p");

            if (contentNodes == null || contentNodes.Count == 0)
            {
                return (title, string.Empty);
            }

            var contentBuilder = new StringBuilder();
            var maxParagraphs = ConfigurationService.Instance.MaxParagraphs;
            foreach (var node in contentNodes.Take(maxParagraphs))
            {
                var text = node.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                {
                    contentBuilder.AppendLine(text);
                }
            }

            var content = contentBuilder.ToString().Trim();

            // Limit content size for AI processing
            var maxContentChars = ConfigurationService.Instance.MaxContentCharacters;
            if (content.Length > maxContentChars)
            {
                content = content.Substring(0, maxContentChars) + "...";
            }

            return (title, content);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }
}

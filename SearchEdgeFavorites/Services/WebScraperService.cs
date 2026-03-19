// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
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
        var handler = new HttpClientHandler
        {
            UseDefaultCredentials = true,
            PreAuthenticate = true,
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer(),
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        // Add default credentials for intranet zones
        var credCache = new CredentialCache();
        handler.Credentials = credCache;
        handler.UseDefaultCredentials = true;

        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(ConfigurationService.Instance.HttpTimeoutSeconds);

        // Add more browser-like headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    public async Task<(string Title, string Content, int? StatusCode)> FetchPageContentAsync(string url)
    {
        try
        {
            LogMessage($"Fetching URL: {url}");

            var response = await _httpClient.GetAsync(url);
            var statusCode = (int)response.StatusCode;

            LogMessage($"Response Status: {statusCode} {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized || 
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    LogMessage($"Authentication required for {url}. Status: {response.StatusCode}");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    LogMessage($"Page not found (404) - marking as dead: {url}");
                }
                else
                {
                    LogMessage($"HTTP Error {response.StatusCode} for {url}");
                }
                return (string.Empty, string.Empty, statusCode);
            }

            var html = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(html) || html.Length < 100)
            {
                LogMessage($"Received empty or very short response ({html.Length} chars)");
                return (string.Empty, string.Empty, statusCode);
            }

            LogMessage($"Successfully fetched {html.Length} characters");
            var result = ParseHtml(html);

            // Enhanced detection for JavaScript-rendered pages
            if (string.IsNullOrEmpty(result.Content) && html.Length > 1000)
            {
                // Try to extract any meaningful text from the shell
                result = ExtractFromJavaScriptPage(html, url);

                if (!string.IsNullOrEmpty(result.Content))
                {
                    LogMessage($"Extracted {result.Content.Length} chars from JavaScript page using fallback method");
                }
                else
                {
                    LogMessage($"JavaScript-rendered page detected - cannot extract content without browser rendering");
                }
            }

            return (result.Title, result.Content, statusCode);
        }
        catch (HttpRequestException ex)
        {
            LogMessage($"HTTP Request Exception for {url}: {ex.Message}");
            return (string.Empty, string.Empty, null);
        }
        catch (TaskCanceledException ex)
        {
            LogMessage($"Request timeout for {url}: {ex.Message}");
            return (string.Empty, string.Empty, null);
        }
        catch (Exception ex)
        {
            LogMessage($"Unexpected error fetching {url}: {ex.GetType().Name} - {ex.Message}");
            return (string.Empty, string.Empty, null);
        }
    }

    private (string Title, string Content) ExtractFromJavaScriptPage(string html, string url)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty;
            var contentBuilder = new StringBuilder();

            // 1. Extract ALL meta tags for maximum context
            var metaTags = doc.DocumentNode.SelectNodes("//meta[@name or @property]");
            if (metaTags != null)
            {
                foreach (var meta in metaTags)
                {
                    var name = meta.GetAttributeValue("name", "") ?? meta.GetAttributeValue("property", "");
                    var content = meta.GetAttributeValue("content", "");

                    if (!string.IsNullOrWhiteSpace(content) && content.Length > 10)
                    {
                        // Focus on descriptive meta tags
                        if (name.Contains("description") || name.Contains("keywords") || 
                            name.Contains("og:") || name.Contains("twitter:") ||
                            name.Contains("application-name") || name.Contains("subject"))
                        {
                            contentBuilder.AppendLine($"{name}: {content}");
                        }
                    }
                }
            }

            // 2. Extract meaningful URL parameters
            var urlInfo = ExtractUrlContext(url);
            if (!string.IsNullOrEmpty(urlInfo))
            {
                contentBuilder.AppendLine(urlInfo);
            }

            // 3. Look for noscript content
            var noscript = doc.DocumentNode.SelectNodes("//noscript");
            if (noscript != null)
            {
                foreach (var node in noscript)
                {
                    var text = node.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                    {
                        contentBuilder.AppendLine($"Fallback content: {text}");
                    }
                }
            }

            // 4. Extract JSON-LD structured data
            var jsonLd = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLd != null)
            {
                foreach (var script in jsonLd)
                {
                    var json = script.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(json) && json.Length < 2000)
                    {
                        contentBuilder.AppendLine($"Structured data: {json}");
                    }
                }
            }

            // 5. Extract data attributes that might contain app info
            var dataAttributes = doc.DocumentNode.SelectNodes("//*[@data-app-id or @data-app-name or @data-workspace or @data-site-name]");
            if (dataAttributes != null)
            {
                foreach (var elem in dataAttributes)
                {
                    foreach (var attr in elem.Attributes)
                    {
                        if (attr.Name.StartsWith("data-") && !string.IsNullOrWhiteSpace(attr.Value))
                        {
                            contentBuilder.AppendLine($"{attr.Name}: {attr.Value}");
                        }
                    }
                }
            }

            // 6. Look for any visible text in the initial HTML (some SPAs have loading states)
            var bodyText = doc.DocumentNode.SelectSingleNode("//body")?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                // Remove excessive whitespace
                bodyText = System.Text.RegularExpressions.Regex.Replace(bodyText, @"\s+", " ");

                // Extract first meaningful sentences (up to 500 chars)
                if (bodyText.Length > 20 && bodyText.Length < 2000)
                {
                    var preview = bodyText.Length > 500 ? bodyText.Substring(0, 500) + "..." : bodyText;
                    // Only add if it's not just scripts/styles
                    if (!preview.Contains("function(") && !preview.Contains("window.") && 
                        !preview.Contains("require([") && !preview.Contains("define(["))
                    {
                        contentBuilder.AppendLine($"Initial page text: {preview}");
                    }
                }
            }

            // 7. Add domain-specific context as fallback
            if (contentBuilder.Length < 100)
            {
                var lowerUrl = url.ToLowerInvariant();
                if (lowerUrl.Contains("powerapps.com"))
                {
                    contentBuilder.AppendLine("Application Type: Microsoft PowerApps - Low-code business application platform");
                }
                else if (lowerUrl.Contains("dynamics.com"))
                {
                    contentBuilder.AppendLine("Application Type: Microsoft Dynamics 365 - Enterprise resource planning and CRM");
                }
                else if (lowerUrl.Contains("sharepoint.com"))
                {
                    contentBuilder.AppendLine("Application Type: Microsoft SharePoint - Collaboration and document management");
                }
                else if (lowerUrl.Contains("delve.office.com"))
                {
                    contentBuilder.AppendLine("Application Type: Microsoft Delve - Office 365 profile and analytics");
                }
                else
                {
                    contentBuilder.AppendLine("Application Type: Single Page Application (SPA) with dynamic content");
                }
            }

            var finalContent = contentBuilder.ToString().Trim();

            // Limit total content size
            if (finalContent.Length > 2000)
            {
                finalContent = finalContent.Substring(0, 2000) + "...";
            }

            return (title, finalContent);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private string ExtractUrlContext(string url)
    {
        try
        {
            var uri = new Uri(url);
            var context = new StringBuilder();

            // Extract domain/subdomain info
            context.AppendLine($"Domain: {uri.Host}");

            // Parse query parameters for meaningful info
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            foreach (string key in query.Keys)
            {
                if (key != null && !string.IsNullOrWhiteSpace(query[key]))
                {
                    // Focus on descriptive parameters
                    if (key.ToLowerInvariant().Contains("tenant") || 
                        key.ToLowerInvariant().Contains("app") ||
                        key.ToLowerInvariant().Contains("site") ||
                        key.ToLowerInvariant().Contains("workspace") ||
                        key.ToLowerInvariant().Contains("id") ||
                        key.ToLowerInvariant().Contains("name"))
                    {
                        context.AppendLine($"Parameter {key}: {query[key]}");
                    }
                }
            }

            // Extract path segments
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length > 0)
            {
                context.AppendLine($"Path: {string.Join(" > ", pathSegments)}");
            }

            return context.ToString();
        }
        catch
        {
            return string.Empty;
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
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [WebScraper] {message}\n");
        }
        catch
        {
            // Can't log
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

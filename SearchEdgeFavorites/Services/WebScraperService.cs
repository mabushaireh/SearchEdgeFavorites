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

    public async Task<(string Title, string Content, int? StatusCode, string FailureReason)> FetchPageContentAsync(string url)
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
                    return (string.Empty, string.Empty, statusCode, $"Authentication required ({response.StatusCode})");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    LogMessage($"Page not found (404) - marking as dead: {url}");
                    return (string.Empty, string.Empty, statusCode, string.Empty); // Dead, not permanently failed
                }
                else
                {
                    LogMessage($"HTTP Error {response.StatusCode} for {url}");
                    return (string.Empty, string.Empty, statusCode, string.Empty); // Temporary error
                }
            }

            var html = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(html) || html.Length < 100)
            {
                LogMessage($"Received empty or very short response ({html.Length} chars)");
                return (string.Empty, string.Empty, statusCode, string.Empty);
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

            return (result.Title, result.Content, statusCode, string.Empty);
        }
        catch (HttpRequestException ex)
        {
            LogMessage($"HTTP Request Exception for {url}: {ex.Message}");
            // Check for DNS/network errors that are permanent
            if (ex.Message.Contains("No such host is known") || 
                ex.Message.Contains("Name or service not known") ||
                ex.Message.Contains("nodename nor servname provided") ||
                ex.Message.Contains("The remote name could not be resolved"))
            {
                return (string.Empty, string.Empty, null, $"Network error: {ex.Message}");
            }
            return (string.Empty, string.Empty, null, string.Empty);
        }
        catch (TaskCanceledException ex)
        {
            LogMessage($"Request timeout for {url}: {ex.Message}");
            return (string.Empty, string.Empty, null, $"Request timeout ({ConfigurationService.Instance.HttpTimeoutSeconds}s)");
        }
        catch (Exception ex)
        {
            LogMessage($"Unexpected error fetching {url}: {ex.GetType().Name} - {ex.Message}");
            return (string.Empty, string.Empty, null, string.Empty);
        }
    }

    private (string Title, string Content) ExtractFromJavaScriptPage(string html, string url)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1. Extract title (enhanced for Power BI and SPAs)
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty;

            // Clean up title - remove common suffixes/prefixes
            if (!string.IsNullOrEmpty(title))
            {
                title = title.Replace(" - Microsoft Power BI", "")
                            .Replace(" | Microsoft Power BI", "")
                            .Replace(" - Power BI", "")
                            .Trim();
            }

            var contentBuilder = new StringBuilder();

            // 2. Try to extract report/app name from JavaScript variables
            var scriptTags = doc.DocumentNode.SelectNodes("//script[not(@src)]");
            if (scriptTags != null)
            {
                foreach (var script in scriptTags)
                {
                    var scriptContent = script.InnerText;
                    if (string.IsNullOrEmpty(scriptContent)) continue;

                    // Look for common patterns in JavaScript
                    var reportNamePatterns = new[]
                    {
                        @"reportName[""']?\s*[:=]\s*[""']([^""']+)[""']",
                        @"displayName[""']?\s*[:=]\s*[""']([^""']+)[""']",
                        @"title[""']?\s*[:=]\s*[""']([^""']+)[""']",
                        @"""name""\s*:\s*""([^""]+)""",
                        @"appName[""']?\s*[:=]\s*[""']([^""']+)[""']",
                    };

                    foreach (var pattern in reportNamePatterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(scriptContent, pattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (match.Success && match.Groups.Count > 1)
                        {
                            var extractedName = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(extractedName) && 
                                extractedName.Length > 3 && 
                                extractedName.Length < 200 &&
                                !extractedName.Contains("function") &&
                                !extractedName.Contains("{"))
                            {
                                contentBuilder.AppendLine($"Report/App Name: {extractedName}");

                                // Use this as title if current title is empty or generic
                                if (string.IsNullOrEmpty(title) || 
                                    title.Equals("Power BI", StringComparison.OrdinalIgnoreCase) ||
                                    title.Length < 5)
                                {
                                    title = extractedName;
                                }
                                break;
                            }
                        }
                    }
                }
            }

            // 3. Extract ALL meta tags for maximum context
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

            // 4. Extract meaningful URL parameters
            var urlInfo = ExtractUrlContext(url);
            if (!string.IsNullOrEmpty(urlInfo))
            {
                contentBuilder.AppendLine(urlInfo);
            }

            // 5. Look for noscript content
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

            // 6. Extract JSON-LD structured data
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

            // 7. Extract data attributes that might contain app info
            var dataAttributes = doc.DocumentNode.SelectNodes("//*[@data-app-id or @data-app-name or @data-workspace or @data-site-name or @data-report-name]");
            if (dataAttributes != null)
            {
                foreach (var elem in dataAttributes)
                {
                    foreach (var attr in elem.Attributes)
                    {
                        if (attr.Name.StartsWith("data-") && !string.IsNullOrWhiteSpace(attr.Value))
                        {
                            // Skip GUIDs - they're not useful for search
                            if (!Guid.TryParse(attr.Value, out _))
                            {
                                contentBuilder.AppendLine($"{attr.Name}: {attr.Value}");
                            }
                        }
                    }
                }
            }

            // 8. Look for any visible text in the initial HTML (some SPAs have loading states)
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

            // 9. Add domain-specific context as fallback
            if (contentBuilder.Length < 100)
            {
                var lowerUrl = url.ToLowerInvariant();
                if (lowerUrl.Contains("powerbi.com"))
                {
                    var powerBiContext = ExtractPowerBIContext(url);
                    contentBuilder.AppendLine(powerBiContext);
                }
                else if (lowerUrl.Contains("powerapps.com"))
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
                else if (lowerUrl.Contains("microsoftstream.com"))
                {
                    contentBuilder.AppendLine("Application Type: Microsoft Stream - Enterprise video streaming service");
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

    private string ExtractPowerBIContext(string url)
    {
        try
        {
            var context = new StringBuilder();
            context.AppendLine("Application Type: Microsoft Power BI - Business intelligence and analytics");

            // Note: The actual report title should be extracted from the HTML <title> tag
            // which is handled in the parent method. This method provides context only.

            var uri = new Uri(url);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Determine content type from URL structure
            if (url.Contains("/reports/"))
            {
                context.AppendLine("Content Type: Interactive Power BI Report with data visualizations and analytics");
            }
            else if (url.Contains("/dashboards/"))
            {
                context.AppendLine("Content Type: Power BI Dashboard with business metrics tiles");
            }
            else if (url.Contains("/datasets/"))
            {
                context.AppendLine("Content Type: Power BI Dataset");
            }
            else
            {
                context.AppendLine("Content Type: Power BI workspace or app content");
            }

            // Add workspace context if it's a shared workspace
            if (url.Contains("/groups/") && !url.Contains("/groups/me/"))
            {
                context.AppendLine("Location: Shared workspace (team collaboration)");
            }
            else if (url.Contains("/groups/me/"))
            {
                context.AppendLine("Location: Personal workspace");
            }

            return context.ToString();
        }
        catch
        {
            return "Application Type: Microsoft Power BI - Business intelligence report";
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

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SearchEdgeFavorites.Models;

namespace SearchEdgeFavorites.Helpers;

public static class DomainIconHelper
{
    private static DomainIconConfig? _config;
    private static readonly object _lock = new object();

    private static void LogToDebug(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SearchEdgeFavorites",
                "debug.log");

            File.AppendAllText(logPath, 
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [DomainIconHelper] {message}\n");
        }
        catch
        {
            // Silently fail
        }
    }

    private static DomainIconConfig LoadConfig()
    {
        lock (_lock)
        {
            if (_config != null)
            {
                return _config;
            }

            try
            {
                var baseDir = AppContext.BaseDirectory;
                var configPath = Path.Combine(baseDir, "Assets", "DomainIcons.json");

                LogToDebug($"Loading config from: {configPath}");
                LogToDebug($"File exists: {File.Exists(configPath)}");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    LogToDebug($"JSON length: {json.Length} chars");

                    _config = JsonSerializer.Deserialize<DomainIconConfig>(json);

                    if (_config != null)
                    {
                        LogToDebug($"Config loaded successfully! Domains count: {_config.Domains.Count}");
                        return _config;
                    }
                    else
                    {
                        LogToDebug("Config deserialization returned null");
                    }
                }
                else
                {
                    LogToDebug($"Config file not found at: {configPath}");
                }
            }
            catch (Exception ex)
            {
                LogToDebug($"Error loading config: {ex.Message}");
            }

            // Fallback: return default config
            LogToDebug("Using fallback default config");
            _config = new DomainIconConfig
            {
                DefaultIcon = "\uE774",
                DefaultDescription = "Web - Globe icon"
            };

            return _config;
        }
    }

    public static IconInfo GetIconForUrl(string url)
    {
        try
        {
            var config = LoadConfig();
            var uri = new Uri(url);
            var domain = uri.Host.ToLowerInvariant();

            // Removed excessive logging for performance

            // Check each pattern in order
            foreach (var mapping in config.Domains)
            {
                if (domain.Contains(mapping.Pattern.ToLowerInvariant()))
                {
                    return new IconInfo(mapping.Icon);
                }
            }

            // Return default icon if no match found
            return new IconInfo(config.DefaultIcon);
        }
        catch
        {
            // Fallback to generic web icon
            return new IconInfo("\uE774");
        }
    }
}

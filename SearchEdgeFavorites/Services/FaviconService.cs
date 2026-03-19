// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SearchEdgeFavorites.Services;

public class FaviconService
{
    private readonly string _edgeFaviconsDbPath;
    private readonly string _faviconCachePath;

    public FaviconService()
    {
        _edgeFaviconsDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "Edge",
            "User Data",
            "Default",
            "Favicons");

        _faviconCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites",
            "Favicons");

        Directory.CreateDirectory(_faviconCachePath);
    }

    public string? GetFaviconFromEdgeDatabase(string url)
    {
        try
        {
            if (!File.Exists(_edgeFaviconsDbPath))
            {
                return null;
            }

            var uri = new Uri(url);
            var pageUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            var domain = uri.Host.Replace(":", "_");

            // Check if we already cached this favicon
            var cachedPath = Path.Combine(_faviconCachePath, $"{domain}.png");
            if (File.Exists(cachedPath))
            {
                return cachedPath;
            }

            // Query Edge's Favicons database
            using var connection = new SqliteConnection($"Data Source={_edgeFaviconsDbPath};Mode=ReadOnly");
            connection.Open();

            // Edge Favicons database schema:
            // favicons table: id, url, icon_type
            // favicon_bitmaps table: id, icon_id, last_updated, image_data, width, height
            // icon_mapping table: id, page_url, icon_id

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT fb.image_data, fb.width, fb.height
                FROM icon_mapping im
                INNER JOIN favicons f ON im.icon_id = f.id
                INNER JOIN favicon_bitmaps fb ON f.id = fb.icon_id
                WHERE im.page_url LIKE @pageUrl || '%'
                ORDER BY fb.width DESC, fb.last_updated DESC
                LIMIT 1";

            command.Parameters.AddWithValue("@pageUrl", $"{uri.Scheme}://{uri.Host}%");

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    var imageData = (byte[])reader.GetValue(0);

                    // Save to cache
                    File.WriteAllBytes(cachedPath, imageData);
                    return cachedPath;
                }
            }

            return null;
        }
        catch (Exception)
        {
            // Database might be locked by Edge or schema might have changed
            return null;
        }
    }

    public string? GetCachedFaviconPath(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.Replace(":", "_");

            // ONLY check cache - never block on database queries
            var pngPath = Path.Combine(_faviconCachePath, $"{domain}.png");
            if (File.Exists(pngPath))
            {
                return pngPath;
            }

            var icoPath = Path.Combine(_faviconCachePath, $"{domain}.ico");
            if (File.Exists(icoPath))
            {
                return icoPath;
            }

            // Don't try Edge database here - it would block the UI
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void QueueFaviconDownload(string url)
    {
        // Queue async extraction from Edge database (non-blocking)
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                GetFaviconFromEdgeDatabase(url);
            }
            catch
            {
                // Silently fail - Edge might have database locked
            }
        });
    }
}

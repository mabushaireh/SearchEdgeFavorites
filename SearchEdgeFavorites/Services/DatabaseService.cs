// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using SearchEdgeFavorites.Models;

namespace SearchEdgeFavorites.Services;

public class DatabaseService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public DatabaseService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchEdgeFavorites");

        Directory.CreateDirectory(appDataPath);
        _dbPath = Path.Combine(appDataPath, "favorites_cache.db");

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        var createTableCmd = _connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS FavoriteCache (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Url TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                AiDescription TEXT,
                PageContent TEXT,
                LastUpdated TEXT NOT NULL,
                IsSummarized INTEGER NOT NULL DEFAULT 0
            )";
        createTableCmd.ExecuteNonQuery();

        // Create index on URL for fast lookups
        var createIndexCmd = _connection.CreateCommand();
        createIndexCmd.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_url ON FavoriteCache(Url)";
        createIndexCmd.ExecuteNonQuery();
    }

    public FavoriteCache? GetCachedFavorite(string url)
    {
        try
        {
            if (_connection == null) return null;

            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Url, Title, AiDescription, PageContent, LastUpdated, IsSummarized 
                FROM FavoriteCache 
                WHERE Url = @url";
            cmd.Parameters.AddWithValue("@url", url);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new FavoriteCache
                {
                    Id = reader.GetInt32(0),
                    Url = reader.GetString(1),
                    Title = reader.GetString(2),
                    AiDescription = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    PageContent = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    LastUpdated = DateTime.Parse(reader.GetString(5)),
                    IsSummarized = reader.GetInt32(6) == 1
                };
            }
        }
        catch
        {
            // Return null if lookup fails
        }

        return null;
    }

    public void UpsertCache(FavoriteCache cache)
    {
        try
        {
            if (_connection == null) return;

            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FavoriteCache (Url, Title, AiDescription, PageContent, LastUpdated, IsSummarized)
                VALUES (@url, @title, @description, @content, @updated, @summarized)
                ON CONFLICT(Url) DO UPDATE SET
                    Title = @title,
                    AiDescription = @description,
                    PageContent = @content,
                    LastUpdated = @updated,
                    IsSummarized = @summarized";

            cmd.Parameters.AddWithValue("@url", cache.Url);
            cmd.Parameters.AddWithValue("@title", cache.Title);
            cmd.Parameters.AddWithValue("@description", cache.AiDescription ?? string.Empty);
            cmd.Parameters.AddWithValue("@content", cache.PageContent ?? string.Empty);
            cmd.Parameters.AddWithValue("@updated", cache.LastUpdated.ToString("o"));
            cmd.Parameters.AddWithValue("@summarized", cache.IsSummarized ? 1 : 0);

            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Silently fail if database update fails
        }
    }

    public List<string> GetUnsummarizedUrls(int limit = 10)
    {
        var urls = new List<string>();
        try
        {
            if (_connection == null) return urls;

            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Url 
                FROM FavoriteCache 
                WHERE IsSummarized = 0 
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                urls.Add(reader.GetString(0));
            }
        }
        catch
        {
            // Return empty list if query fails
        }

        return urls;
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}

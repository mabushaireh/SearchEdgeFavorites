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
                IsSummarized INTEGER NOT NULL DEFAULT 0,
                IsDead INTEGER NOT NULL DEFAULT 0,
                HttpStatusCode INTEGER
            )";
        createTableCmd.ExecuteNonQuery();

        // Add new columns if they don't exist (for existing databases)
        try
        {
            var alterCmd1 = _connection.CreateCommand();
            alterCmd1.CommandText = "ALTER TABLE FavoriteCache ADD COLUMN IsDead INTEGER NOT NULL DEFAULT 0";
            alterCmd1.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists
        }

        try
        {
            var alterCmd2 = _connection.CreateCommand();
            alterCmd2.CommandText = "ALTER TABLE FavoriteCache ADD COLUMN HttpStatusCode INTEGER";
            alterCmd2.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists
        }

        try
        {
            var alterCmd3 = _connection.CreateCommand();
            alterCmd3.CommandText = "ALTER TABLE FavoriteCache ADD COLUMN IsPermanentlyFailed INTEGER NOT NULL DEFAULT 0";
            alterCmd3.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists
        }

        try
        {
            var alterCmd4 = _connection.CreateCommand();
            alterCmd4.CommandText = "ALTER TABLE FavoriteCache ADD COLUMN FailureReason TEXT";
            alterCmd4.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists
        }

        try
        {
            var alterCmd5 = _connection.CreateCommand();
            alterCmd5.CommandText = "ALTER TABLE FavoriteCache ADD COLUMN Path TEXT";
            alterCmd5.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists
        }

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
                SELECT Id, Url, Title, Path, AiDescription, PageContent, LastUpdated, IsSummarized, IsDead, HttpStatusCode, IsPermanentlyFailed, FailureReason
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
                    Path = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    AiDescription = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    PageContent = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    LastUpdated = DateTime.Parse(reader.GetString(6)),
                    IsSummarized = reader.GetInt32(7) == 1,
                    IsDead = reader.IsDBNull(8) ? false : reader.GetInt32(8) == 1,
                    HttpStatusCode = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    IsPermanentlyFailed = reader.IsDBNull(10) ? false : reader.GetInt32(10) == 1,
                    FailureReason = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
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
                INSERT INTO FavoriteCache (Url, Title, Path, AiDescription, PageContent, LastUpdated, IsSummarized, IsDead, HttpStatusCode, IsPermanentlyFailed, FailureReason)
                VALUES (@url, @title, @path, @description, @content, @updated, @summarized, @dead, @statusCode, @permanentlyFailed, @failureReason)
                ON CONFLICT(Url) DO UPDATE SET
                    Title = @title,
                    Path = @path,
                    AiDescription = @description,
                    PageContent = @content,
                    LastUpdated = @updated,
                    IsSummarized = @summarized,
                    IsDead = @dead,
                    HttpStatusCode = @statusCode,
                    IsPermanentlyFailed = @permanentlyFailed,
                    FailureReason = @failureReason";

            cmd.Parameters.AddWithValue("@url", cache.Url);
            cmd.Parameters.AddWithValue("@title", cache.Title);
            cmd.Parameters.AddWithValue("@path", cache.Path ?? string.Empty);
            cmd.Parameters.AddWithValue("@description", cache.AiDescription ?? string.Empty);
            cmd.Parameters.AddWithValue("@content", cache.PageContent ?? string.Empty);
            cmd.Parameters.AddWithValue("@updated", cache.LastUpdated.ToString("o"));
            cmd.Parameters.AddWithValue("@summarized", cache.IsSummarized ? 1 : 0);
            cmd.Parameters.AddWithValue("@dead", cache.IsDead ? 1 : 0);
            cmd.Parameters.AddWithValue("@statusCode", cache.HttpStatusCode.HasValue ? (object)cache.HttpStatusCode.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@permanentlyFailed", cache.IsPermanentlyFailed ? 1 : 0);
            cmd.Parameters.AddWithValue("@failureReason", cache.FailureReason ?? string.Empty);

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
                WHERE IsSummarized = 0 AND IsDead = 0 AND IsPermanentlyFailed = 0
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

    public SqliteCommand? CreateCommand()
    {
        return _connection?.CreateCommand();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}

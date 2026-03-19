// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SearchEdgeFavorites.Models;

public class BookmarkRoot
{
    [JsonPropertyName("roots")]
    public BookmarkRoots? Roots { get; set; }
}

public class BookmarkRoots
{
    [JsonPropertyName("bookmark_bar")]
    public BookmarkFolder? BookmarkBar { get; set; }

    [JsonPropertyName("other")]
    public BookmarkFolder? Other { get; set; }

    [JsonPropertyName("synced")]
    public BookmarkFolder? Synced { get; set; }
}

public class BookmarkFolder
{
    [JsonPropertyName("children")]
    public List<BookmarkNode>? Children { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class BookmarkNode
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("children")]
    public List<BookmarkNode>? Children { get; set; }
}

public class Favorite
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class FavoriteCache
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AiDescription { get; set; } = string.Empty;
    public string PageContent { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public bool IsSummarized { get; set; }
    public bool IsDead { get; set; }
    public int? HttpStatusCode { get; set; }
}

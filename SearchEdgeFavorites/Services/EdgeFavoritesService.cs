// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SearchEdgeFavorites.Models;

namespace SearchEdgeFavorites.Services;

public class EdgeFavoritesService
{
    private readonly string _bookmarksPath;

    public EdgeFavoritesService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _bookmarksPath = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Bookmarks");
    }

    public List<Favorite> GetFavorites()
    {
        var favorites = new List<Favorite>();

        if (!File.Exists(_bookmarksPath))
        {
            return favorites;
        }

        try
        {
            var json = File.ReadAllText(_bookmarksPath);
            var bookmarkRoot = JsonSerializer.Deserialize<BookmarkRoot>(json);

            if (bookmarkRoot?.Roots != null)
            {
                if (bookmarkRoot.Roots.BookmarkBar != null)
                {
                    ExtractFavorites(bookmarkRoot.Roots.BookmarkBar, "Favorites Bar", favorites);
                }

                if (bookmarkRoot.Roots.Other != null)
                {
                    ExtractFavorites(bookmarkRoot.Roots.Other, "Other Favorites", favorites);
                }

                if (bookmarkRoot.Roots.Synced != null)
                {
                    ExtractFavorites(bookmarkRoot.Roots.Synced, "Synced Favorites", favorites);
                }
            }
        }
        catch
        {
            // Return empty list if parsing fails
        }

        return favorites;
    }

    private void ExtractFavorites(BookmarkFolder folder, string parentPath, List<Favorite> favorites)
    {
        if (folder.Children == null)
        {
            return;
        }

        foreach (var node in folder.Children)
        {
            if (node.Type == "url" && !string.IsNullOrEmpty(node.Url))
            {
                favorites.Add(new Favorite
                {
                    Name = node.Name ?? "Untitled",
                    Url = node.Url,
                    Path = parentPath
                });
            }
            else if (node.Type == "folder" && node.Children != null)
            {
                var nodeName = node.Name ?? "Untitled Folder";
                var newPath = string.IsNullOrEmpty(parentPath) ? nodeName : $"{parentPath} > {nodeName}";
                ExtractFavoritesFromNode(node, newPath, favorites);
            }
        }
    }

    private void ExtractFavoritesFromNode(BookmarkNode node, string parentPath, List<Favorite> favorites)
    {
        if (node.Children == null)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            if (child.Type == "url" && !string.IsNullOrEmpty(child.Url))
            {
                favorites.Add(new Favorite
                {
                    Name = child.Name ?? "Untitled",
                    Url = child.Url,
                    Path = parentPath
                });
            }
            else if (child.Type == "folder" && child.Children != null)
            {
                var childName = child.Name ?? "Untitled Folder";
                var newPath = $"{parentPath} > {childName}";
                ExtractFavoritesFromNode(child, newPath, favorites);
            }
        }
    }
}

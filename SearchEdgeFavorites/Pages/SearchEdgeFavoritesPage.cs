// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SearchEdgeFavorites.Services;
using Windows.Foundation;
using Windows.System;

namespace SearchEdgeFavorites;

internal sealed partial class SearchEdgeFavoritesPage : ListPage
{
    private readonly EdgeFavoritesService _favoritesService;

    public SearchEdgeFavoritesPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Edge Favorites";
        Name = "Open";
        PlaceholderText = "Search favorites...";
        _favoritesService = new EdgeFavoritesService();
    }

    public override IListItem[] GetItems()
    {
        var favorites = _favoritesService.GetFavorites();

        if (!favorites.Any())
        {
            return [
                new ListItem(new NoOpCommand()) 
                { 
                    Title = "No favorites found",
                    Subtitle = "Make sure Microsoft Edge has bookmarks saved" 
                }
            ];
        }

        return favorites.Select(fav => new ListItem(new OpenUrlCommand(fav.Url))
        {
            Title = fav.Name,
            Subtitle = fav.Url
        }).ToArray();
    }
}

internal class OpenUrlCommand : ICommand
{
    private readonly string _url;

    public OpenUrlCommand(string url)
    {
        _url = url;
        Id = _url;
        Name = "Open URL";
    }

    public string Id { get; }
    public string Name { get; }
    public IIconInfo? Icon => null;

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

    public void Invoke()
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "SearchEdgeFavorites", "debug.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"\n{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Attempting to open URL: {_url}\n");

            // Method 1: Windows.System.Launcher
            try
            {
                var uri = new Uri(_url);
                File.AppendAllText(logPath, $"  Method 1: Windows.System.Launcher with URI: {uri.AbsoluteUri}\n");
                var task = Launcher.LaunchUriAsync(uri).AsTask();
                task.Wait();
                File.AppendAllText(logPath, $"  Method 1: SUCCESS\n");
                return;
            }
            catch (Exception ex1)
            {
                File.AppendAllText(logPath, $"  Method 1 FAILED: {ex1.GetType().Name} - {ex1.Message}\n");
            }

            // Method 2: cmd.exe /c start
            try
            {
                File.AppendAllText(logPath, $"  Method 2: cmd.exe /c start\n");
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{_url}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(psi);
                File.AppendAllText(logPath, $"  Method 2: Process started - {process?.Id}\n");
                return;
            }
            catch (Exception ex2)
            {
                File.AppendAllText(logPath, $"  Method 2 FAILED: {ex2.GetType().Name} - {ex2.Message}\n");
            }

            // Method 3: Direct Process.Start
            try
            {
                File.AppendAllText(logPath, $"  Method 3: Direct Process.Start\n");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = _url,
                    UseShellExecute = true
                });
                File.AppendAllText(logPath, $"  Method 3: SUCCESS - {process?.Id}\n");
            }
            catch (Exception ex3)
            {
                File.AppendAllText(logPath, $"  Method 3 FAILED: {ex3.GetType().Name} - {ex3.Message}\n");
            }
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(logPath, $"CRITICAL ERROR: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}\n");
            }
            catch
            {
                // Can't even log - fail silently
            }
        }
    }
}

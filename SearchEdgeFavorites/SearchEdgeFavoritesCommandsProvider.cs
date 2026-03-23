// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SearchEdgeFavorites;

public partial class SearchEdgeFavoritesCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public SearchEdgeFavoritesCommandsProvider()
    {
        DisplayName = "Edge Favorites AI";
        Icon = new IconInfo("🤖");
        _commands = [
            new CommandItem(new SearchEdgeFavoritesPage()) { Title = DisplayName, Icon = Icon },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}

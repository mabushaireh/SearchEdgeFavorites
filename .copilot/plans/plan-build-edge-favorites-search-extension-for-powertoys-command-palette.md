# 🎯 Build Edge Favorites Search Extension for PowerToys Command Palette

## Understanding
Create a PowerToys Command Palette extension that reads Microsoft Edge favorites/bookmarks from the local file system, displays them as searchable list items, and opens the selected URL in the default browser when invoked.

## Assumptions
- Edge favorites are stored in `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Bookmarks` as JSON
- The bookmarks JSON structure follows Chromium's format (nested folders with bookmark entries)
- Users have the default Edge profile or we'll search for available profiles
- Opening URLs will use the default system browser via Process.Start

## Approach
Edge stores bookmarks in a JSON file at `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Bookmarks`. The file contains a nested structure of folders and bookmark entries. We'll:
1. Create model classes to deserialize the JSON structure
2. Build a service to read, parse, and flatten the bookmark tree into a searchable list
3. Update [SearchEdgeFavoritesPage.cs](SearchEdgeFavorites/Pages/SearchEdgeFavoritesPage.cs) to load favorites and return them as list items
4. Implement the command action to open URLs using Process.Start
5. Test the extension

## Key Files
- `SearchEdgeFavorites/Pages/SearchEdgeFavoritesPage.cs` - main page that displays favorites list
- New: `SearchEdgeFavorites/Models/BookmarkModels.cs` - models for deserializing Edge bookmarks JSON
- New: `SearchEdgeFavorites/Services/EdgeFavoritesService.cs` - service to read and parse favorites

## Risks & Open Questions
- Need to handle cases where Edge bookmarks file doesn't exist or is inaccessible
- Should support multiple Edge profiles if needed
- Bookmark structure may vary slightly between Edge versions

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-18 17:11:03

## 📝 Plan Steps
- ✅ **Create bookmark model classes to match Edge's JSON structure**
- ✅ **Create EdgeFavoritesService to read and parse bookmarks file**
- ✅ **Update SearchEdgeFavoritesPage to load favorites from service**
- ✅ **Implement URL opening command for each favorite item**
- ✅ **Test the extension with deployment and reload**


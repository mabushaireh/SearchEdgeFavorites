# Edge Favorites AI - PowerToys Command Palette Extension

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A powerful PowerToys Command Palette extension that enhances Microsoft Edge favorites with AI-powered descriptions, intelligent search, and comprehensive bookmark management.

## Features

### Core Features
- AI-Powered Descriptions: Automatically generates keyword summaries using OpenAI GPT-4 or Google Gemini
- Smart Search: Full-text search across bookmark names, URLs, folder paths, and AI descriptions
- Folder Path Tracking: Displays complete folder hierarchy (e.g., "Favorites Bar > Teams > Development")
- SQLite Caching: Fast local cache prevents redundant API calls
- Favicon Support: Website icons for easy visual identification
- Custom Domain Icons: Special icons for popular services

### Bookmark Management
- Sync Favorites: Clean database and remove dead bookmarks from Edge
- Consistency Check: Detailed comparison between database and Edge
- Dead Link Detection: Automatically identifies and marks 404 pages
- Permanent Failure Tracking: Marks URLs with authentication errors, DNS failures

### Reliability & Performance
- Smart Throttling: Configurable delays between requests
- Retry Logic: Automatic retries with exponential backoff
- Session Limits: Configurable limits per session
- Comprehensive Logging: Detailed debug logs
- Error Recovery: Graceful handling of network issues

## Quick Start

### Prerequisites
- Windows 11 with PowerToys installed
- .NET 9 SDK
- Microsoft Edge with bookmarks
- OpenAI API key OR Google Gemini API key

### Installation

1. Clone the repository:
   ```powershell
   git clone https://github.com/mabushaireh/SearchEdgeFavorites.git
   cd SearchEdgeFavorites
   ```

2. Build the project:
   ```powershell
   dotnet restore
   dotnet build -c Release
   ```

3. Configure API keys (see Configuration section)

4. Register with PowerToys Command Palette (automatic after build)

## Configuration

### Configuration File Location
%LOCALAPPDATA%\SearchEdgeFavorites\config.txt

### Complete Configuration Options

# AI Provider Selection
AI_PROVIDER=openai                    # Options: openai, gemini
OPENAI_API_KEY=sk-...
GEMINI_API_KEY=...

# AI Model Settings
OPENAI_MODEL=gpt-4o-mini             # gpt-4, gpt-4o, gpt-4o-mini, gpt-3.5-turbo
GEMINI_MODEL=gemini-1.5-flash        # gemini-1.5-pro, gemini-1.5-flash, gemini-pro

# Processing Limits
MAX_AI_SUMMARIES_PER_SESSION=5       # Default: 5
MAX_SCRAPING_ATTEMPTS=20             # Default: 20
MAX_RATE_LIMIT_RETRIES=5             # Default: 5

# Network Settings
HTTP_TIMEOUT_SECONDS=15              # Default: 15
DELAY_BETWEEN_REQUESTS_MS=2000       # Default: 2000ms

# Cache Settings
CACHE_EXPIRY_DAYS=30                 # Default: 30

# Logging
ENABLE_DEBUG_LOGGING=true            # Default: true

## Usage

### Basic Search
1. Open PowerToys Command Palette (Alt+Space)
2. Type to search
3. Start typing to search favorites
4. Results show bookmark name, AI description, and favicon

### Sync Favorites
1. Open PowerToys Command Palette
2. Select "Sync Favorites"
3. Review log at: %LOCALAPPDATA%\SearchEdgeFavorites\sync_result.log

### Check Consistency
1. Open PowerToys Command Palette
2. Select "Check Consistency"
3. Review detailed report

## Logging & Diagnostics

### Log File Locations
All logs: %LOCALAPPDATA%\SearchEdgeFavorites\

### debug.log
- AI summarization and background processing
- Session timesta
mps and configuration
- URLs being processed
- AI summary results
- HTTP status codes
- Error messages and stack traces

### sync_result.log
- Bookmark synchronization details
- Dead URLs removed
- Missing URLs cleaned up
- New URLs added

### url_open.log
- URL opening activity
- Timestamps and success/failure status

### Database Location
%LOCALAPPDATA%\SearchEdgeFavorites\favorites_cache.db

Schema:
- URL (unique)
- Title
- Path (folder hierarchy)
- AiDescription
- PageContent
- LastUpdated
- IsSummarized
- IsDead (404 pages)
- HttpStatusCode
- IsPermanentlyFailed
- FailureReason

## Troubleshooting

### No AI descriptions appearing
- Verify API key in config.txt
- Check debug.log for errors
- Increase MAX_AI_SUMMARIES_PER_SESSION
- Delete database to force re-summarization

### Rate limit errors
- Increase DELAY_BETWEEN_REQUESTS_MS
- Reduce MAX_AI_SUMMARIES_PER_SESSION
- Increase MAX_RATE_LIMIT_RETRIES

### Slow performance
- Reduce MAX_AI_SUMMARIES_PER_SESSION
- Increase CACHE_EXPIRY_DAYS
- Let cache build over multiple sessions

## Project Structure

SearchEdgeFavorites/
├── Services/
│   ├── EdgeFavoritesService.cs       # Reads Edge bookmarks
│   ├── DatabaseService.cs            # SQLite operations
│   ├── WebScraperService.cs          # HTTP fetching
│   ├── OpenAiSummaryService.cs       # OpenAI integration
│   ├── GeminiSummaryService.cs       # Gemini integration
│   ├── UnifiedAiService.cs           # AI orchestration
│   ├── CacheUpdateService.cs         # Background processing
│   ├── FavoritesSyncService.cs       # Synchronization
│   ├── FaviconService.cs             # Favicon handling
│   └── ConfigurationService.cs       # Config management
├── Pages/
│   └── SearchEdgeFavoritesPage.cs    # Command Palette UI
├── Models/
│   ├── BookmarkModels.cs             # Data models
│   └── DomainIconConfig.cs           # Icon mappings
└── Helpers/
    └── DomainIconHelper.cs           # Icon resolution

## How It Works

1. Reads Edge bookmarks from JSON file
2. Fetches page content with HTTP client
3. Parses HTML and extracts meaningful content
4. Sends to AI for keyword summarization
5. Caches results in SQLite database
6. Provides fast search interface

## Performance

- Initial scan: Instant (<100ms)
- AI summaries: Configurable per session
- Background processing: Non-blocking
- Cache hit: <10ms
- Search: Fast SQLite indexing

## Privacy & Security

- All data stored locally
- API keys in plaintext (secure your config.txt)
- Page content shared with AI provider
- No telemetry or data collection

## Development

Build & Run:
```powershell
git clone https://github.com/mabushaireh/SearchEdgeFavorites.git
cd SearchEdgeFavorites
dotnet restore
dotnet build -c Release
```

## Contributing

Contributions welcome! Areas for contribution:
- Additional AI providers
- UI improvements
- Performance optimizations
- Better error handling
- Documentation improvements

## License

Copyright (c) Microsoft Corporation. Licensed under the MIT License.

## Credits

Built using:
- .NET 9
- PowerToys Command Palette
- OpenAI API
- Google Gemini API
- HtmlAgilityPack
- Microsoft.Data.Sqlite

## Support

- Documentation: https://github.com/mabushaireh/SearchEdgeFavorites/wiki
- Report Issues: https://github.com/mabushaireh/SearchEdgeFavorites/issues
- Discussions: https://github.com/mabushaireh/SearchEdgeFavorites/discussions

---

Made with love for productive browsing by mabushaireh

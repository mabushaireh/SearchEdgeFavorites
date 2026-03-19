# SearchEdgeFavorites

A **PowerToys Command Palette extension** that provides AI-powered search for Microsoft Edge favorites/bookmarks with intelligent content summarization and dead link management.

## Features

- 🔍 **Smart Search**: Search Edge bookmarks by name, URL, folder, or AI-generated content summaries
- 🤖 **AI-Powered Summaries**: Automatically generates compressed, keyword-rich summaries of bookmarked pages using OpenAI or Google Gemini
- 💾 **Intelligent Caching**: Stores page content and AI summaries locally to minimize API calls
- 🔄 **Sync & Cleanup**: Automatically sync database with Edge favorites and remove dead bookmarks (404s)
- ☠️ **Dead Link Detection**: Automatically marks 404 pages and other dead links to avoid repeated processing
- 🔐 **Windows Authentication**: Full support for internal/corporate sites with Windows Integrated Authentication
- ⚡ **JavaScript Page Support**: Detects and extracts metadata from JavaScript-rendered SPAs (SharePoint, PowerApps, Dynamics)
- ⚙️ **Fully Configurable**: Customize AI providers, rate limits, timeouts, and more
- 📊 **Comprehensive Logging**: Detailed debug logs for troubleshooting and monitoring

## Prerequisites

- **Windows 11** with [PowerToys](https://learn.microsoft.com/windows/powertoys/) installed
- **.NET 9 SDK** (for building from source)
- **Microsoft Edge** (with bookmarks/favorites)
- **AI Provider** (at least one):
  - OpenAI API key (GPT-3.5), or
  - Google Gemini API key

## Installation

### Option 1: Install from Release (Coming Soon)
1. Download the latest `.msix` package from [Releases](https://github.com/mabushaireh/SearchEdgeFavorites/releases)
2. Double-click the `.msix` file to install
3. Open PowerToys Command Palette (Ctrl+Shift+P)
4. Your extension will be available

### Option 2: Build from Source

```powershell
# Clone the repository
git clone https://github.com/mabushaireh/SearchEdgeFavorites.git
cd SearchEdgeFavorites

# Build the project
dotnet build -c Release

# Deploy the extension
# In Visual Studio: right-click project > Deploy
# Or use F5 to debug
```

### Setting up PowerToys

1. Install [PowerToys](https://github.com/microsoft/PowerToys/releases) if not already installed
2. Enable **Command Palette** in PowerToys settings
3. Set your preferred keyboard shortcut (default: Ctrl+Shift+P)
4. Deploy this extension

## Configuration

Create a configuration file at:
```
%LOCALAPPDATA%\SearchEdgeFavorites\config.txt
```

### Required Configuration

At minimum, configure one AI provider:

```ini
# Option 1: Use OpenAI
OPENAI_API_KEY=sk-proj-your-openai-api-key-here
AI_PROVIDER=openai

# Option 2: Use Google Gemini
GEMINI_API_KEY=AIzaSy-your-gemini-api-key-here
AI_PROVIDER=gemini

# Option 3: Use both (will try both if one fails)
OPENAI_API_KEY=sk-proj-your-openai-api-key-here
GEMINI_API_KEY=AIzaSy-your-gemini-api-key-here
AI_PROVIDER=both
```

### Optional Configuration

Customize behavior with these optional settings (defaults shown):

```ini
# AI Summary Limits
MAX_AI_SUMMARIES_PER_SESSION=5          # Number of summaries to generate per session
MAX_RATE_LIMIT_RETRIES=5                # Retries for rate-limited API calls
DELAY_BETWEEN_REQUESTS_MS=2000          # Delay between AI requests (milliseconds)

# Web Scraping Limits
MAX_SCRAPING_ATTEMPTS=20                # Max URLs to attempt scraping per session
HTTP_TIMEOUT_SECONDS=10                 # Timeout for fetching web pages
MAX_PARAGRAPHS=20                       # Max paragraphs to extract from pages
MAX_CONTENT_CHARACTERS=4000             # Max characters to send to AI

# Cache Settings
CACHE_EXPIRY_DAYS=7                     # Days before re-summarizing a bookmark
```

### Getting API Keys

**OpenAI API Key:**
1. Visit [OpenAI Platform](https://platform.openai.com/api-keys)
2. Sign up or log in
3. Create a new API key
4. Copy the key (starts with `sk-proj-...`)

**Google Gemini API Key:**
1. Visit [Google AI Studio](https://aistudio.google.com/app/apikey)
2. Sign in with your Google account
3. Click "Create API Key"
4. Copy the key (starts with `AIzaSy...`)

## Usage

### 1. Search Edge Favorites

1. Press **Ctrl+Shift+P** (or your PowerToys Command Palette shortcut)
2. Type "**Edge Favorites**"
3. Select "**Search Edge Favorites**"
4. Start typing to search by:
   - Bookmark name
   - URL
   - Folder path
   - AI-generated content summary

### 2. Sync Favorites ⇄ Database

The sync command keeps your Edge bookmarks and cached database perfectly synchronized.

**What it does:**
- ✅ **Removes DB entries** for bookmarks deleted from Edge
- ✅ **Removes dead bookmarks** from Edge (404s marked in database)
- ✅ **Creates automatic backups** before modifying Edge bookmarks
- ✅ **Logs all operations** for review

**How to run:**
1. Press **Ctrl+Shift+P** (PowerToys Command Palette)
2. Type "**Sync Favorites**"
3. Select "**Sync Favorites ⇄ Database**"
4. A log file opens automatically showing results

**Example output:**
```
2026-03-19 01:35:00 - Starting Favorites Sync
============================================================
Current favorites count: 475
Cached URLs in database: 550

--- Cleaning Database ---
  ✓ Removed from DB: https://old-deleted-site.com/page
  ✓ Removed from DB: https://another-gone-page.com
Total removed from database: 75

--- Dead URLs in Database: 5 ---

--- Removing Dead Bookmarks from Edge ---
  ✓ Backup created: Bookmarks.backup.20260319013500
  ☠ Removing dead bookmark: Employee Discounts (https://...)
  ☠ Removing dead bookmark: Old Tool (https://...)
  ✓ Bookmarks file updated
Total removed from favorites: 5

============================================================
Sync completed successfully!
  - Removed from database: 75
  - Removed from favorites: 5
```

**When to run:**
- After manually cleaning up Edge favorites
- Periodically (weekly/monthly) to remove dead links
- When database feels out of sync with current bookmarks

## How It Works

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Edge Bookmarks File                      │
│          %LOCALAPPDATA%\Microsoft\Edge\User Data\           │
│                    Default\Bookmarks                         │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│              EdgeFavoritesService                            │
│          Reads and parses Edge bookmarks                     │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│            CacheUpdateService                                │
│  • Processes favorites in background                         │
│  • Skips already-cached and dead pages                       │
│  • Respects rate limits (configurable)                       │
└─────────────────┬───────────────────────────────────────────┘
                  │
     ┌────────────┼────────────┐
     ▼            ▼             ▼
┌─────────┐  ┌──────────┐  ┌───────────┐
│  Web    │  │ Database │  │    AI     │
│ Scraper │  │ Service  │  │  Service  │
└─────────┘  └──────────┘  └───────────┘
     │            │             │
     └────────────┴─────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│              SQLite Database                                 │
│     %LOCALAPPDATA%\SearchEdgeFavorites\                     │
│              favorites_cache.db                              │
│                                                              │
│  Stores:                                                     │
│  • Page titles and content snippets                          │
│  • AI-generated summaries                                    │
│  • HTTP status codes                                         │
│  • Dead link markers (IsDead flag)                           │
│  • Last update timestamps                                    │
└──────────────────────────────────────────────────────────────┘
```

### Web Scraping Process

1. **HTTP Request**: Sends authenticated request with Windows credentials
2. **Status Code Check**: 
   - `200 OK` → Process content
   - `404 Not Found` → Mark as dead, never retry
   - `401/403` → Cache error, retry later
   - `Timeout` → Cache failure, retry later
3. **Content Extraction**:
   - **Static HTML**: Parse with HtmlAgilityPack
   - **JavaScript SPAs**: Extract metadata, structured data, and fallback content
4. **AI Summarization**: Send extracted content to OpenAI or Gemini
5. **Database Storage**: Cache results for fast future searches

### Dead Link Management

**Automatic Detection:**
- HTTP 404 responses are automatically marked as `IsDead = true`
- Dead pages are **never processed again** (saves time and API costs)
- HTTP status codes are logged for debugging

**Manual Cleanup:**
- Run **Sync Favorites** command to remove dead bookmarks from Edge
- Creates automatic backup before modification
- All changes are logged to `sync.log`

### Authentication Handling

**Supported Methods:**
- ✅ Windows Integrated Authentication (NTLM, Kerberos)
- ✅ Cookie-based sessions
- ✅ Pre-authentication headers
- ✅ SSL certificate bypass (for internal CAs)

**Browser-Like Headers:**
The scraper mimics Edge browser to avoid detection:
- User-Agent: Edge/Chrome
- Accept, Accept-Language, Accept-Encoding
- Connection: keep-alive
- DNT (Do Not Track)

## Logging & Debugging

All logs are stored in: `%LOCALAPPDATA%\SearchEdgeFavorites\`

### Log Files

| File | Purpose |
|------|---------|
| `debug.log` | Main application log with detailed session information |
| `sync.log` | Sync operations (DB cleanup, dead link removal) |
| `sync_result.log` | Quick summary of last sync operation |
| `url_open.log` | URL opening attempts and results |

### Debug Log Format

```
================================================================================
2026-03-19 01:28:35 - SESSION START: Cache Update
================================================================================
Configuration:
  - Total favorites to process: 475
  - Target AI summaries per session: 5
  - Max scraping attempts: 20
  - HTTP timeout: 10s
  - Delay between requests: 2000ms
  - Cache expiry: 7 days
  - AI Provider: openai

  [Skipped] Dead page (HTTP 404): https://old-site.com/page
  [Attempt #1] Processing: https://example.com/page1
2026-03-19 01:28:36 [WebScraper] Fetching URL: https://example.com/page1
2026-03-19 01:28:36 [WebScraper] Response Status: 200 OK
2026-03-19 01:28:36 [WebScraper] Successfully fetched 15432 characters
2026-03-19 01:28:36 [WebScraper] Extracted 1243 chars from JavaScript page
      Fetched content, generating AI summary...
2026-03-19 01:28:37 - Attempting summary with OpenAI GPT-3.5
2026-03-19 01:28:38 - ✓ Success with OpenAI GPT-3.5
      ✓ AI Summary #1: Comprehensive guide to...

================================================================================
2026-03-19 01:29:00 - SESSION END: Cache Update Completed
================================================================================
Summary:
  - URLs attempted: 20
  - URLs skipped (cached/dead): 15
  - AI summaries generated: 5
  - Total cached: 450/475
  - Remaining unsummarized: 25
================================================================================
```

### Common Issues & Solutions

**Issue: "Failed to fetch content (may require authentication)"**
- ✅ Check if URL requires VPN or corporate network
- ✅ Verify Windows credentials have access
- ✅ Increase `HTTP_TIMEOUT_SECONDS` for slow sites

**Issue: "No AI summaries generated"**
- ✅ Check API keys in `config.txt`
- ✅ Verify API key has credits/quota remaining
- ✅ Check `debug.log` for specific API errors

**Issue: "Too many dead pages"**
- ✅ Run **Sync Favorites** to clean up Edge bookmarks
- ✅ Check if corporate URLs changed domains
- ✅ Review `debug.log` for HTTP status codes

## Database Schema

```sql
CREATE TABLE FavoriteCache (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Url TEXT NOT NULL UNIQUE,
    Title TEXT NOT NULL,
    AiDescription TEXT,              -- AI-generated summary
    PageContent TEXT,                -- Extracted page content
    LastUpdated TEXT NOT NULL,       -- ISO 8601 timestamp
    IsSummarized INTEGER NOT NULL,   -- 1 = has AI summary, 0 = pending
    IsDead INTEGER NOT NULL,         -- 1 = 404 or dead, 0 = alive
    HttpStatusCode INTEGER           -- Last HTTP status (200, 404, etc.)
);

CREATE INDEX idx_url ON FavoriteCache(Url);
```

## Performance & Costs

### Typical Session
- **Favorites**: 500 bookmarks
- **Configuration**: 5 summaries per session, 20 attempts
- **Time**: ~30 seconds per session
- **API Calls**: 5 per session
- **Cost**: ~$0.01 per session (OpenAI GPT-3.5)

### Optimization Features
- ✅ Only processes new/expired bookmarks
- ✅ Skips dead pages permanently
- ✅ Respects rate limits with delays
- ✅ Caches everything locally
- ✅ Configurable session limits

## Privacy & Security

- ✅ **Local Storage**: All data stored locally on your PC
- ✅ **No Cloud Sync**: Bookmarks and summaries never leave your machine (except AI API calls)
- ✅ **Encrypted API Keys**: Should be stored securely (use environment variables in production)
- ✅ **Windows Credentials**: Used automatically for corporate sites
- ✅ **Automatic Backups**: Created before modifying Edge bookmarks

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Submit a pull request

## License

MIT License - See LICENSE file for details

## Support

- **Issues**: [GitHub Issues](https://github.com/mabushaireh/SearchEdgeFavorites/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mabushaireh/SearchEdgeFavorites/discussions)
- **Logs**: Check `%LOCALAPPDATA%\SearchEdgeFavorites\debug.log`

## Acknowledgments

- Built with [Windows App SDK](https://docs.microsoft.com/windows/apps/windows-app-sdk/)
- AI powered by [OpenAI](https://openai.com/) and [Google Gemini](https://ai.google.dev/)
- HTML parsing by [HtmlAgilityPack](https://html-agility-pack.net/)
4. Copy the key (starts with `AIzaSy...`)

## Usage

### Opening the Extension

1. Open **Windows DevHome**
2. Press `Ctrl+Shift+P` or click the Command Palette
3. Look for **"Edge Favorites AI 🤖"**
4. Click or press Enter

### Searching Bookmarks

- **Type to search**: Enter any text to search bookmark names, URLs, and folders
- **Content search**: Search by page content keywords (after AI summaries are generated)
- **Click to open**: Click any bookmark to open it in Edge

### How AI Summaries Work

1. **First Run**: Extension scans your Edge bookmarks
2. **Background Processing**: Automatically fetches and summarizes up to 5 bookmarks per session
3. **Smart Caching**: Summaries are cached for 7 days (configurable)
4. **Incremental Updates**: Each time you open the extension, it processes more uncached bookmarks

### Viewing Logs

Debug logs are stored at:
```
%LOCALAPPDATA%\SearchEdgeFavorites\debug.log
```

Logs include:
- API key configuration status
- AI summary generation results
- Web scraping attempts
- Error messages

### Database Location

The cache database (SQLite) is stored at:
```
%LOCALAPPDATA%\SearchEdgeFavorites\favorites_cache.db
```

## Troubleshooting

### "OpenAI API key not configured" error

**Symptoms**: Log shows API key not configured even though it's set

**Solution**:
1. Check config file location: `%LOCALAPPDATA%\SearchEdgeFavorites\config.txt`
2. Ensure no leading/trailing spaces on the line
3. Use exact format: `OPENAI_API_KEY=your-key-here`
4. Restart the extension

### No AI summaries appearing

**Possible causes**:
1. **No API key configured**: Check logs for configuration errors
2. **Corporate network blocking**: Some corporate firewalls block AI APIs
3. **Rate limits**: Extension limits to 5 summaries per session by default
4. **Invalid API key**: Check your API key is valid and has credits

**Solutions**:
- Verify API key is correct in config.txt
- Check debug.log for specific error messages
- Increase `MAX_AI_SUMMARIES_PER_SESSION` if needed
- Add billing info to your AI provider account

### Extension not appearing in DevHome

**Solutions**:
1. Restart DevHome
2. Check Windows Event Viewer for extension errors
3. Rebuild the project: `dotnet clean && dotnet build -c Release`
4. Verify .NET 9 SDK is installed

### Rate limit errors

**Solution**: Adjust configuration:
```ini
MAX_RATE_LIMIT_RETRIES=10
DELAY_BETWEEN_REQUESTS_MS=5000
MAX_AI_SUMMARIES_PER_SESSION=3
```

## Project Structure

```
SearchEdgeFavorites/
├── Services/
│   ├── EdgeFavoritesService.cs      # Reads Edge bookmarks
│   ├── DatabaseService.cs           # SQLite cache management
│   ├── WebScraperService.cs         # Fetches page content
│   ├── OpenAiSummaryService.cs      # OpenAI integration
│   ├── GeminiSummaryService.cs      # Google Gemini integration
│   ├── UnifiedAiService.cs          # AI provider orchestration
│   ├── CacheUpdateService.cs        # Background processing
│   └── ConfigurationService.cs      # Configuration management
├── Pages/
│   └── SearchEdgeFavoritesPage.cs   # Search UI
├── Models/
│   └── BookmarkModels.cs            # Data models
└── README.md                        # This file
```

## How It Works

1. **Bookmark Reading**: Reads Edge bookmarks from `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Bookmarks`
2. **Content Fetching**: Uses HTTP client to fetch page content (respects timeouts and user-agent)
3. **HTML Parsing**: Extracts meaningful content from HTML using HtmlAgilityPack
4. **AI Summarization**: Sends content to OpenAI or Gemini for compressed keyword summaries
5. **Caching**: Stores summaries in SQLite database to avoid redundant API calls
6. **Search**: Full-text search across bookmark names, URLs, folders, and AI summaries

## Performance

- **Initial scan**: Instant (reads Edge bookmarks file)
- **AI summaries**: 5 per session (configurable)
- **Background processing**: Non-blocking, won't freeze UI
- **Cache hit**: Instant results for previously summarized bookmarks
- **Search**: Fast full-text search with SQLite

## Privacy & Security

- ✅ All data stored locally on your machine
- ✅ API keys stored in plaintext config file (secure your `config.txt`)
- ✅ Only sends page content to AI providers (you choose the provider)
- ✅ No telemetry or data collection
- ⚠️ Page content is shared with your configured AI provider

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

### Development Setup

```powershell
# Clone the repo
git clone https://github.com/mabushaireh/SearchEdgeFavorites.git
cd SearchEdgeFavorites

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests (if available)
dotnet test
```

## License

Copyright (c) Microsoft Corporation. Licensed under the MIT License. See [LICENSE](LICENSE) file for details.

## Credits

Built using:
- [.NET 9](https://dotnet.microsoft.com/)
- [Windows DevHome](https://github.com/microsoft/devhome)
- [OpenAI API](https://openai.com/api/)
- [Google Gemini API](https://ai.google.dev/)
- [HtmlAgilityPack](https://html-agility-pack.net/)
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite/)

## Support

- 📖 [Documentation](https://github.com/mabushaireh/SearchEdgeFavorites/wiki)
- 🐛 [Report Issues](https://github.com/mabushaireh/SearchEdgeFavorites/issues)
- 💬 [Discussions](https://github.com/mabushaireh/SearchEdgeFavorites/discussions)

---

Made with ❤️ for productive browsingdirect commit test

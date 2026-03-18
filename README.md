# SearchEdgeFavorites

A Windows DevHome extension that provides AI-powered search for Microsoft Edge favorites/bookmarks with intelligent content summarization.

## Features

- 🔍 **Smart Search**: Search Edge bookmarks by name, URL, folder, or AI-generated content summaries
- 🤖 **AI-Powered Summaries**: Automatically generates compressed, keyword-rich summaries of bookmarked pages using OpenAI or Google Gemini
- 💾 **Intelligent Caching**: Stores page content and AI summaries locally to minimize API calls
- ⚙️ **Fully Configurable**: Customize AI providers, rate limits, timeouts, and more
- 🔄 **Background Processing**: Automatically processes bookmarks in the background without blocking

## Prerequisites

- **Windows 11** (with DevHome installed)
- **.NET 9 SDK** (for building from source)
- **Microsoft Edge** (with bookmarks/favorites)
- **AI Provider** (at least one):
  - OpenAI API key (GPT-3.5), or
  - Google Gemini API key

## Installation

### Option 1: Install from Release (Coming Soon)
1. Download the latest `.msix` package from [Releases](https://github.com/mabushaireh/SearchEdgeFavorites/releases)
2. Double-click the `.msix` file to install
3. The extension will appear in Windows DevHome

### Option 2: Build from Source

```powershell
# Clone the repository
git clone https://github.com/mabushaireh/SearchEdgeFavorites.git
cd SearchEdgeFavorites

# Build the project
dotnet build -c Release

# Deploy to DevHome
# The extension will be available in DevHome after building
```

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
MAX_SCRAPING_ATTEMPTS=100               # Max URLs to attempt scraping per session
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

Made with ❤️ for productive browsing
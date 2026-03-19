# Performance Guide - SearchEdgeFavorites Extension

## Problem: UI Blocking & Unresponsive Extension

### Root Causes That Can Block UI
1. **Synchronous Database Queries** - Multiple DB lookups during GetItems()
2. **Excessive Logging** - Writing logs for every item (467+ writes)
3. **Icon Loading** - Querying Edge's locked Favicons database
4. **Background Processing** - Cache updates blocking main thread

---

## Best Practices to Prevent UI Blocking

### 1. Keep GetItems() Fast (< 100ms)

**❌ BAD:**
`csharp
foreach (var fav in favorites)
{
    var cached = _databaseService.GetCachedFavorite(fav.Url); // BLOCKS!
}
`

**✅ GOOD:**
`csharp
// Use in-memory cache or batch query
var allCached = _databaseService.GetAllCached(); // One query
`

### 2. Move Heavy Work to Background

**✅ Always use Task.Run for expensive operations:**
`csharp
Task.Run(() => _cacheUpdateService.QueueUrlsForProcessing(favorites));
`

### 3. Add Try-Catch Boundaries

**Every item should fail gracefully:**
`csharp
foreach (var fav in favorites)
{
    try { /* process */ }
    catch { continue; } // Skip bad items
}
`

### 4. Limit Logging in Loops

**❌ BAD:** Log every item (467 file writes)  
**✅ GOOD:** Log summaries only

### 5. Use Timeouts for External Resources

`csharp
connection.Timeout = TimeSpan.FromSeconds(1);
`

### 6. Cache Config Files

Load heavy JSON once, cache in memory.

---

## Performance Monitoring

Add timing to GetItems():
`csharp
var sw = Stopwatch.StartNew();
// ... work ...
if (sw.ElapsedMilliseconds > 100)
    LogWarning("GetItems took {sw.ElapsedMilliseconds}ms"");
`

---

## Checklist Before Adding Code

- [ ] Will this block UI? → Use Task.Run
- [ ] Querying DB per item? → Batch or cache
- [ ] Logging in loops? → Remove or summarize
- [ ] Can this fail? → Add try-catch
- [ ] External resource? → Add timeout
- [ ] 100+ items? → Consider pagination

---

## Emergency Fix Pattern

If UI freezes:
1. Comment out DB/network calls
2. Rebuild and test
3. Re-enable with background threads

---

_Quick Reference Guide - Last Updated: March 19, 2026_

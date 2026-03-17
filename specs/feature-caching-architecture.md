# Spec: feature-caching-architecture

**Project:** Boutquin.Trading (engine) + ETFWealthIQ (consumer)
**Status:** DRAFT
**Created:** 2026-03-17
**Version:** 1.0.0
**Type:** Feature
**Frozen-at:** none

**References:**
- Conventions: `~/.claude/conventions/dotnet-conventions.md`
- Project context: `Boutquin.Trading/CLAUDE.md`
- Downstream spec: `~/Developer/etfwealthiq/specs/feature-data-loader.md` (PL-02)
- Existing patterns: `src/Domain/Data/CsvMarketDataFetcher.cs`, `src/Domain/Data/CsvMarketDataStorage.cs`
- DI registration: `src/Application/Configuration/ServiceCollectionExtensions.cs`

---

## Summary

The Boutquin.Trading engine has **zero caching** at every data boundary. Every fetcher (`TiingoFetcher`, `FrankfurterFetcher`, `FredFetcher`, `FamaFrenchFetcher`) performs fresh HTTP requests on every invocation. `BackTest.RunAsync` re-fetches all market data and FX rates per run. `SimulatedBrokerage.SubmitOrderAsync` calls `FetchMarketDataAsync` per order. `WalkForwardOptimizer` amplifies this across 5-20 folds. `FamaFrenchFetcher` downloads and decompresses an 8MB+ ZIP every call.

This spec introduces a **three-layer caching architecture** using the decorator pattern around existing fetcher interfaces. The design is:

1. **L1 — In-process memory cache** (session-scoped): Eliminates redundant fetches within a single backtest run and across WalkForward/MonteCarlo folds.
2. **L2 — CSV file cache** (disk-persisted): Eliminates redundant API calls across runs. Uses the existing `CsvMarketDataFetcher`/`CsvMarketDataStorage` patterns plus new CSV classes for FRED and Fama-French data (from PL-02).
3. **Backtest-level data prefetch**: `BackTest.RunAsync` buffers all data once and passes it to `SimulatedBrokerage` instead of per-order re-fetching.

**Key design constraint:** The caching layer is transparent — it implements the same interfaces as the underlying fetchers. Consumers (BackTest, Portfolio, strategies) require zero changes. The decorator is wired in DI or at construction time.

---

## Problem Statement

### Quantified Impact

| Scenario | Current API Calls | With Caching |
|----------|------------------|--------------|
| Single backtest, 40 tickers, 5 FX pairs | 45 HTTP requests | 0 (L1 warm) or 45 (L2 miss, first run) |
| 10 backtest iterations (parameter tuning) | 450 HTTP requests | 45 (L2 cache) |
| WalkForward 10 folds | 450 HTTP requests | 45 (L1 warm across folds) |
| FamaFrench factor regression | 1 ZIP download (~8MB) per call | 0 (L1) or 1 (L2 miss) |
| SimulatedBrokerage, 200 orders | 200 `FetchMarketDataAsync` calls | 0 (prefetched data) |
| ETFWealthIQ pipeline, 10 blueprints | 450 × 10 = 4,500 HTTP requests | 45 (L2 shared across blueprints) |

### Root Causes

1. **No caching decorator exists** — each fetcher is a direct HTTP client
2. **`BackTest.RunAsync`** fetches at lines 120, 130 — no data reuse across runs
3. **`SimulatedBrokerage.SubmitOrderAsync`** calls `FetchMarketDataAsync` at line 82 — per-order fetching
4. **No L2 storage exists** for FRED economic data or Fama-French factor data
5. **`CsvMarketDataStorage`** cannot write FX rates — only equity OHLCV

---

## Architecture

### Layer Diagram

```
Consumer (BackTest, Portfolio, Strategy)
  │
  ▼
CachingMarketDataFetcher (L1 memory)          ← new, implements IMarketDataFetcher
  │
  ▼
CsvMarketDataFetcher (L2 disk)                ← existing
  │
  ▼
CompositeMarketDataFetcher (API)              ← existing
  ├── TiingoFetcher
  └── FrankfurterFetcher

CachingEconomicDataFetcher (L1 memory)        ← new, implements IEconomicDataFetcher
  │
  ▼
CsvEconomicDataFetcher (L2 disk)              ← new (from PL-02)
  │
  ▼
FredFetcher (API)                             ← existing

CachingFactorDataFetcher (L1 memory)          ← new, implements IFactorDataFetcher
  │
  ▼
CsvFactorDataFetcher (L2 disk)                ← new (from PL-02)
  │
  ▼
FamaFrenchFetcher (API)                       ← existing
```

### Design Principles

1. **Decorator pattern** — Caching wrappers implement the same interface as the inner fetcher. No new interfaces.
2. **Write-through on L2 miss** — When L2 (CSV) misses, fetch from API, write to L2, populate L1, return to caller.
3. **L1 is session-scoped** — The `ConcurrentDictionary` lives as long as the caching decorator instance. Typical lifetime: one pipeline execution or one DI scope.
4. **L2 is persistent** — CSV files survive process restarts. Freshness is managed externally (ETFWealthIQ's `--max-age-days` flag or manual deletion).
5. **No cache invalidation complexity** — Historical financial data is immutable. New data is additive (append to equity CSVs, overwrite for FRED/FF).
6. **Transparent to consumers** — `BackTest`, `Portfolio`, strategies see `IMarketDataFetcher` — they don't know about caching.

### L2 Write Idempotency Contract

Each data type has a specific write mode based on how its source provides data:

| Data Type | Write Mode | Idempotency | Rationale |
|-----------|-----------|-------------|-----------|
| **Equity OHLCV** | Append (check last date) | Re-running skips existing dates; appends only new rows | Tiingo returns incremental daily data; append avoids re-downloading full history |
| **FX rates** | Overwrite (atomic) | Re-running replaces entire file; safe to repeat | Frankfurter returns complete series; append would create duplicates |
| **FRED economic** | Overwrite (atomic) | Re-running replaces entire file; safe to repeat | FRED returns complete series for a given date range |
| **Fama-French factors** | Overwrite (atomic) | Re-running replaces entire file; safe to repeat | FF downloads are complete dataset ZIPs |

**Atomic write protocol for overwrite-mode data types:**

1. Write to a temporary file in the same directory: `{target}.tmp`
2. On successful completion, rename `{target}.tmp` → `{target}` (atomic on POSIX, near-atomic on Windows NTFS)
3. On failure (exception, cancellation), delete `{target}.tmp` if it exists
4. This ensures a CSV file either contains complete data or doesn't exist — no partial files

**Append-mode guard for equity data:**

1. If CSV exists, read the last line's date
2. Fetch from API starting at `lastDate + 1 day`
3. Append new rows (same `FileMode.OpenOrCreate` pattern as existing `CsvMarketDataStorage`)
4. If CSV doesn't exist, fetch full history and write with header

**Concurrent access:** Write-through decorators are not thread-safe for writes to the same CSV file. This is acceptable because:
- L1 (memory cache) sits above L2 — concurrent requests for the same data hit L1 and never reach L2 simultaneously
- The `Lazy<Task<...>>` pattern in L1 ensures exactly-once materialization, which means exactly-one write-through to L2
- If L1 is disabled and two threads request the same uncached data, `FileShare.None` on the write side causes the second writer to fail with `IOException` — the caller retries and finds the CSV from the first writer (L2 hit)

---

## Acceptance Criteria

### L1 Memory Cache

- [ ] `CachingMarketDataFetcher` implements `IMarketDataFetcher`; second call with same symbols returns cached data without calling inner fetcher
- [ ] `CachingEconomicDataFetcher` implements `IEconomicDataFetcher`; second call with same `seriesId` returns cached data
- [ ] `CachingFactorDataFetcher` implements `IFactorDataFetcher`; second call with same `dataset` returns cached data
- [ ] L1 cache is thread-safe (`ConcurrentDictionary`)
- [ ] L1 cache key includes all discriminating parameters (symbols, seriesId+dateRange, dataset+frequency)
- [ ] `IDisposable` clears L1 cache and disposes inner fetcher if disposable

### L2 CSV Cache (Write-Through)

- [ ] `WriteThroughMarketDataFetcher` implements `IMarketDataFetcher`; on L2 miss, fetches from API, writes to CSV, returns data
- [ ] `WriteThroughEconomicDataFetcher` implements `IEconomicDataFetcher`; on L2 miss, fetches from API, writes to CSV
- [ ] `WriteThroughFactorDataFetcher` implements `IFactorDataFetcher`; on L2 miss, fetches from API, writes to CSV
- [ ] L2 existence check: if CSV file exists for the requested data, reads from CSV instead of API
- [ ] FX rate CSV writing works (new method on `CsvMarketDataStorage` or dedicated class)

### Backtest Data Prefetch

- [ ] `BackTest.RunAsync` materializes market data and FX rates once and passes buffered data to `SimulatedBrokerage`
- [ ] `SimulatedBrokerage` accepts a `IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>` for pre-buffered lookups
- [ ] Per-order fetch (`SimulatedBrokerage.cs:82`) is eliminated when pre-buffered data is available
- [ ] Backward-compatible: existing constructor without buffered data still works (falls back to per-order fetch)

### DI Integration

- [ ] `ServiceCollectionExtensions.AddBoutquinTrading` registers caching decorators when `CacheOptions` is configured
- [ ] `CacheOptions` provides `DataDirectory` (for L2 CSV path) and `EnableMemoryCache` (for L1)
- [ ] When `CacheOptions.DataDirectory` is null, L2 is skipped (pure L1 or no caching)

### Quality

- [ ] `dotnet build` succeeds with zero warnings (TreatWarningsAsErrors)
- [ ] `dotnet test` passes all existing + new tests
- [ ] No breaking changes to existing interfaces or constructors

---

## Deliverables

### Phase 1: L1 Memory Cache Decorators

| File | Action | Purpose |
|------|--------|---------|
| `src/Application/Caching/CachingMarketDataFetcher.cs` | Create | L1 memory cache for `IMarketDataFetcher` |
| `src/Application/Caching/CachingEconomicDataFetcher.cs` | Create | L1 memory cache for `IEconomicDataFetcher` |
| `src/Application/Caching/CachingFactorDataFetcher.cs` | Create | L1 memory cache for `IFactorDataFetcher` |
| `tests/UnitTests/Application/Caching/CachingMarketDataFetcherTests.cs` | Create | Unit tests |
| `tests/UnitTests/Application/Caching/CachingEconomicDataFetcherTests.cs` | Create | Unit tests |
| `tests/UnitTests/Application/Caching/CachingFactorDataFetcherTests.cs` | Create | Unit tests |

### Phase 2: L2 Write-Through Decorators

| File | Action | Purpose |
|------|--------|---------|
| `src/Application/Caching/WriteThroughMarketDataFetcher.cs` | Create | L2 CSV write-through for `IMarketDataFetcher` |
| `src/Application/Caching/WriteThroughEconomicDataFetcher.cs` | Create | L2 CSV write-through for `IEconomicDataFetcher` |
| `src/Application/Caching/WriteThroughFactorDataFetcher.cs` | Create | L2 CSV write-through for `IFactorDataFetcher` |
| `src/Domain/Data/CsvMarketDataStorage.cs` | Update | Add FX rate CSV writing method |
| `src/Data.CSV/MarketDataFileNameHelper.cs` | Update | Add naming helpers for economic and factor CSVs |
| `tests/UnitTests/Application/Caching/WriteThroughMarketDataFetcherTests.cs` | Create | Unit tests |
| `tests/UnitTests/Application/Caching/WriteThroughEconomicDataFetcherTests.cs` | Create | Unit tests |
| `tests/UnitTests/Application/Caching/WriteThroughFactorDataFetcherTests.cs` | Create | Unit tests |

**Dependency:** Phase 2 requires `CsvEconomicDataFetcher`, `CsvEconomicDataStorage`, `CsvFactorDataFetcher`, `CsvFactorDataStorage` from PL-02 Phase 1. If PL-02 is not yet implemented, Phase 2 covers only market data + FX write-through.

### Phase 3: Backtest Data Prefetch

| File | Action | Purpose |
|------|--------|---------|
| `src/Application/Brokers/SimulatedBrokerage.cs` | Update | Add constructor accepting pre-buffered market data |
| `src/Application/Backtest.cs` | Update | Materialize data once, pass to brokerage |
| `tests/UnitTests/Application/SimulatedBrokerageTests.cs` | Update | Test pre-buffered path |
| `tests/UnitTests/Application/BacktestTests.cs` | Update | Test data prefetch behavior |

### Phase 4: DI Wiring

| File | Action | Purpose |
|------|--------|---------|
| `src/Application/Configuration/CacheOptions.cs` | Create | Options class for cache configuration |
| `src/Application/Configuration/ServiceCollectionExtensions.cs` | Update | Register caching decorators |

---

## Relevant Files

| File | Why |
|------|-----|
| `src/Domain/Interfaces/IMarketDataFetcher.cs` | Interface the L1/L2 decorators must implement |
| `src/Domain/Interfaces/IEconomicDataFetcher.cs` | Interface the FRED cache decorators must implement |
| `src/Domain/Interfaces/IFactorDataFetcher.cs` | Interface the FF cache decorators must implement |
| `src/Domain/Interfaces/IMarketDataStorage.cs` | Interface for equity CSV writing (extend for FX) |
| `src/Domain/Data/CsvMarketDataFetcher.cs` | L2 read pattern for equity + FX |
| `src/Domain/Data/CsvMarketDataStorage.cs` | L2 write pattern for equity; needs FX extension |
| `src/Data.CSV/MarketDataFileNameHelper.cs` | File naming convention |
| `src/Application/Backtest.cs` | Lines 120, 130 — data fetch; needs prefetch refactor |
| `src/Application/Brokers/SimulatedBrokerage.cs` | Line 82 — per-order fetch; needs buffered path |
| `src/Application/CompositeMarketDataFetcher.cs` | Existing composite; L1/L2 wraps this |
| `src/Application/Configuration/ServiceCollectionExtensions.cs` | DI registration |
| `src/Application/Analytics/WalkForwardOptimizer.cs` | Consumer that benefits from L1 (multiple folds) |
| `src/Application/Analytics/MonteCarloSimulator.cs` | Consumer that benefits from L1 |

---

## Step-by-Step Tasks

### Phase 1: L1 Memory Cache Decorators

#### 1.1 Create `CachingMarketDataFetcher`

Create `src/Application/Caching/CachingMarketDataFetcher.cs`:

```csharp
public sealed class CachingMarketDataFetcher : IMarketDataFetcher, IDisposable
{
    private readonly IMarketDataFetcher _inner;
    private readonly ConcurrentDictionary<string, List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>> _marketDataCache = new();
    private readonly ConcurrentDictionary<string, List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>> _fxCache = new();
    private readonly ILogger<CachingMarketDataFetcher> _logger;

    public CachingMarketDataFetcher(IMarketDataFetcher inner, ILogger<CachingMarketDataFetcher> logger);
    // Backward-compatible:
    public CachingMarketDataFetcher(IMarketDataFetcher inner);
}
```

**Cache key strategy for market data:**
- Sort and join symbol tickers: `"AAPL|MSFT|VTI"` — normalized, deterministic
- `FetchMarketDataAsync`: first call materializes the `IAsyncEnumerable` to a `List`, stores in `_marketDataCache`, yields from list. Subsequent calls with same key yield from cached list.

**Cache key strategy for FX rates:**
- Sort and join currency pairs: `"USD_CAD|USD_EUR"`

**Important:** The cache stores materialized lists, not raw `IAsyncEnumerable`. This is safe because:
- Historical data is finite (at most ~6000 trading days × N symbols)
- Backtests always materialize the full stream anyway (`await foreach` in `BackTest.RunAsync`)
- Memory footprint for 40 tickers × 5800 days ≈ ~50MB — acceptable for session scope

**Thread safety:** `ConcurrentDictionary.GetOrAdd` with a factory that materializes the stream. The factory itself is not thread-safe (IAsyncEnumerable is single-consumer), so use `Lazy<Task<List<...>>>` as the cache value to ensure single materialization.

```csharp
// Cache value type:
ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>>>> _marketDataCache;
```

This ensures exactly-once materialization even under concurrent access.

#### 1.2 Create `CachingEconomicDataFetcher`

Create `src/Application/Caching/CachingEconomicDataFetcher.cs`:

```csharp
public sealed class CachingEconomicDataFetcher : IEconomicDataFetcher, IDisposable
{
    private readonly IEconomicDataFetcher _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, decimal>>>>> _cache = new();
}
```

**Cache key:** `"{seriesId}|{startDate}|{endDate}"` — includes date range because the interface accepts optional filters. `null` dates are represented as `"*"`.

**Date filter optimization:** If a cached entry covers a wider date range than requested, filter in-memory instead of re-fetching. This handles the common pattern where the first call fetches full history and subsequent calls request subsets.

```csharp
// Superset check:
// If cached key is "DGS10|*|*" and request is "DGS10|2020-01-01|2023-12-31",
// filter the cached list instead of calling _inner.
```

#### 1.3 Create `CachingFactorDataFetcher`

Create `src/Application/Caching/CachingFactorDataFetcher.cs`:

```csharp
public sealed class CachingFactorDataFetcher : IFactorDataFetcher, IDisposable
{
    private readonly IFactorDataFetcher _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>> _dailyCache = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>>>> _monthlyCache = new();
}
```

**Cache key:** `"{dataset}|{startDate}|{endDate}"` for daily; same for monthly (separate dictionaries).

Same superset-check optimization as economic data.

#### 1.4 Write L1 Unit Tests

Test each caching decorator:

1. **Cache hit:** Call twice with same args → inner fetcher called once, both calls return same data
2. **Cache miss:** Call with different args → inner fetcher called for each
3. **Thread safety:** Parallel calls with same args → inner fetcher called exactly once
4. **Superset filter:** Fetch full range, then request subset → returns filtered data without calling inner
5. **Dispose:** Clears cache, disposes inner if `IDisposable`
6. **Cancellation:** CancellationToken propagated to inner fetcher

Use `Moq` to verify inner fetcher call counts. Use in-memory test data (no HTTP).

---

### Phase 2: L2 Write-Through Decorators

**Prerequisite:** PL-02 Phase 1 (CsvEconomicDataFetcher/Storage, CsvFactorDataFetcher/Storage). If not yet available, implement Phase 2 for market data only.

#### 2.1 Extend `MarketDataFileNameHelper`

Update `src/Data.CSV/MarketDataFileNameHelper.cs` — add:

```csharp
public static string GetCsvFileNameForEconomicData(string directory, string seriesId)
    => Path.Combine(directory, $"fred_{SanitizeTickerForFileName(seriesId)}.csv");

public static string GetCsvFileNameForFactorData(string directory, FamaFrenchDataset dataset, string frequency)
    => Path.Combine(directory, $"ff_{dataset}_{SanitizeTickerForFileName(frequency)}.csv");
```

Make `SanitizeTickerForFileName` internal (currently private) so the new naming methods can reuse it. Or extract a shared `SanitizeFileName` method.

#### 2.2 Extend `CsvMarketDataStorage` for FX Rates

Update `src/Domain/Data/CsvMarketDataStorage.cs` — add:

```csharp
public async Task SaveFxRatesAsync(
    string currencyPair,
    IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> rates,
    CancellationToken cancellationToken = default)
{
    var filePath = MarketDataFileNameHelper.GetCsvFileNameForFxRateData(_dataDirectory, currencyPair);
    // Overwrite mode (not append — FX is fetched as complete series)
    await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
    await using var writer = new StreamWriter(fileStream);
    await writer.WriteLineAsync("Date,Rate").ConfigureAwait(false);

    await foreach (var kvp in rates.WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        var line = FormattableString.Invariant($"{kvp.Key},{kvp.Value}");
        await writer.WriteLineAsync(line).ConfigureAwait(false);
    }
}
```

Note: This method doesn't fit `IMarketDataStorage` (which takes `SortedDictionary<Asset, MarketData>`). Add it directly to `CsvMarketDataStorage` as a concrete method. No new interface needed — the write-through decorator holds a concrete `CsvMarketDataStorage` reference.

#### 2.3 Create `WriteThroughMarketDataFetcher`

Create `src/Application/Caching/WriteThroughMarketDataFetcher.cs`:

```csharp
public sealed class WriteThroughMarketDataFetcher : IMarketDataFetcher, IDisposable
{
    private readonly IMarketDataFetcher _apiFetcher;    // TiingoFetcher/FrankfurterFetcher
    private readonly CsvMarketDataFetcher _csvFetcher;   // L2 reader
    private readonly CsvMarketDataStorage _csvStorage;   // L2 writer
    private readonly string _dataDirectory;
    private readonly ILogger<WriteThroughMarketDataFetcher> _logger;
}
```

**FetchMarketDataAsync logic:**

```
For each symbol in request:
  1. Check if CSV file exists: MarketDataFileNameHelper.GetCsvFileNameForMarketData(dir, ticker)
  2. If exists → read from CsvMarketDataFetcher (L2 hit)
  3. If missing → fetch from _apiFetcher, write to CSV via _csvStorage, return data
```

**Important:** The per-symbol CSV existence check avoids reading all-or-nothing. If 39 of 40 tickers are cached, only the 40th hits the API.

**FetchFxRatesAsync logic:**
Same pattern: check CSV existence per pair, read from CSV or fetch+write.

#### 2.4 Create `WriteThroughEconomicDataFetcher`

Create `src/Application/Caching/WriteThroughEconomicDataFetcher.cs`:

Same pattern as market data. Uses `CsvEconomicDataFetcher` (L2 reader) + `CsvEconomicDataStorage` (L2 writer) + `FredFetcher` (API).

**FetchSeriesAsync logic:**

```
1. Check if fred_{seriesId}.csv exists
2. If exists → read from CsvEconomicDataFetcher, apply date filters
3. If missing → fetch from FredFetcher, write to CsvEconomicDataStorage, return data
```

#### 2.5 Create `WriteThroughFactorDataFetcher`

Create `src/Application/Caching/WriteThroughFactorDataFetcher.cs`:

Same pattern. Uses `CsvFactorDataFetcher` + `CsvFactorDataStorage` + `FamaFrenchFetcher`.

#### 2.6 Write L2 Unit Tests

For each write-through decorator:

1. **L2 hit:** Pre-create CSV file → decorator reads from CSV, never calls API fetcher
2. **L2 miss + write-through:** No CSV → decorator calls API, writes CSV, returns data
3. **Round-trip:** Write-through creates CSV → subsequent call reads from CSV → data matches
4. **Partial cache:** Some symbols cached, some not → API called only for missing
5. **Error handling:** API failure → exception propagates, no partial CSV written (atomic write or cleanup)
6. **FX rate writing:** Verify `SaveFxRatesAsync` creates correct CSV format

---

### Phase 3: Backtest Data Prefetch

#### 3.1 Add Buffered Constructor to `SimulatedBrokerage`

Update `src/Application/Brokers/SimulatedBrokerage.cs`:

```csharp
// Pre-buffered market data for backtest mode — eliminates per-order FetchMarketDataAsync calls
private readonly IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>>? _bufferedMarketData;

/// <summary>
/// Initializes with pre-buffered market data for backtest mode.
/// When buffered data is provided, SubmitOrderAsync looks up data from the buffer
/// instead of calling FetchMarketDataAsync per order.
/// </summary>
public SimulatedBrokerage(
    IMarketDataFetcher marketDataFetcher,
    ITransactionCostModel costModel,
    ISlippageModel? slippageModel,
    IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> bufferedMarketData)
    : this(marketDataFetcher, costModel, slippageModel)
{
    _bufferedMarketData = bufferedMarketData ?? throw new ArgumentNullException(nameof(bufferedMarketData));
}
```

Update `SubmitOrderAsync`:

```csharp
public async Task<bool> SubmitOrderAsync(Order order, CancellationToken cancellationToken)
{
    Guard.AgainstNull(() => order);
    cancellationToken.ThrowIfCancellationRequested();

    MarketData? assetMarketData;

    if (_bufferedMarketData != null)
    {
        // Buffered path: O(1) dictionary lookup instead of IAsyncEnumerable scan
        if (!_bufferedMarketData.TryGetValue(order.Timestamp, out var dayData) ||
            !dayData.TryGetValue(order.Asset, out assetMarketData))
        {
            return false;
        }
    }
    else
    {
        // Original path: fetch from market data source
        var marketData = await _marketDataFetcher.FetchMarketDataAsync([order.Asset], cancellationToken)
            .FirstOrDefaultAsync(kvp => kvp.Key == order.Timestamp, cancellationToken).ConfigureAwait(false);

        if (marketData.Value == null || !marketData.Value.TryGetValue(order.Asset, out assetMarketData))
        {
            return false;
        }
    }

    var isOrderFilled = order.OrderType switch { ... }; // unchanged
    return isOrderFilled;
}
```

Existing constructors are unchanged — backward compatible.

#### 3.2 Add `SetBufferedMarketData` to `IBrokerage`

Update `src/Domain/Interfaces/IBrokerage.cs` — add a default interface method:

```csharp
/// <summary>
/// Provides pre-buffered market data for backtest mode, eliminating per-order fetch calls.
/// Default implementation is a no-op — only SimulatedBrokerage overrides this.
/// </summary>
void SetBufferedMarketData(IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> data) { }
```

This is a C# 8+ default interface method. Existing `IBrokerage` implementations (including any future live brokerage) are unaffected — they inherit the no-op default.

Update `src/Application/Brokers/SimulatedBrokerage.cs` — override to store the buffer:

```csharp
public void SetBufferedMarketData(IReadOnlyDictionary<DateOnly, SortedDictionary<Asset, MarketData>> data)
{
    _bufferedMarketData = data ?? throw new ArgumentNullException(nameof(data));
}
```

Change `_bufferedMarketData` from `readonly` to mutable to support both the constructor path (3.1) and the `SetBufferedMarketData` path.

#### 3.3 Refactor `BackTest.RunAsync` for Data Prefetch

Update `src/Application/Backtest.cs`:

The current flow already materializes FX rates into `fxRatesForDate` (lines 136-139). Extend this to also materialize market data, then pass the buffer to each portfolio's brokerage via `SetBufferedMarketData`.

```csharp
public async Task<Tearsheet> RunAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
{
    // ... existing validation ...

    // Fetch and materialize market data (unchanged from current, but stored for brokerage)
    var marketDataTimeline = _marketDataFetcher.FetchMarketDataAsync(symbols, cancellationToken);
    var bufferedMarketData = new SortedDictionary<DateOnly, SortedDictionary<Asset, MarketData>>();

    await foreach (var kvp in marketDataTimeline.WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        if (kvp.Key >= startDate && kvp.Key <= endDate)
        {
            bufferedMarketData[kvp.Key] = kvp.Value;
        }
    }

    // FX rates materialization (unchanged)
    var fxRatesForDate = new SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>>();
    await foreach (var fxRatesOnDate in fxRatesTimeline.WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        fxRatesForDate[fxRatesOnDate.Key] = fxRatesOnDate.Value;
    }

    // Pass buffered market data to brokerages — SimulatedBrokerage stores it,
    // other IBrokerage implementations ignore it (default interface no-op)
    foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
    {
        portfolio.Brokerage.SetBufferedMarketData(bufferedMarketData);
    }

    // Event loop now iterates bufferedMarketData instead of re-streaming
    foreach (var marketData in bufferedMarketData)
    {
        var fxRates = fxRatesForDate.TryGetValue(marketData.Key, out var ratesForDate)
                      ? ratesForDate
                      : [];

        var marketEvent = new MarketEvent(marketData.Key, marketData.Value, fxRates);

        foreach (var portfolio in new[] { _portfolio, _benchmarkPortfolio })
        {
            await portfolio.HandleEventAsync(marketEvent, cancellationToken).ConfigureAwait(false);
            portfolio.UpdateEquityCurve(marketData.Key);
        }
    }

    // ... existing metrics ...
}
```

**Note:** This requires `IPortfolio.Brokerage` to be accessible. If `IPortfolio` does not expose the brokerage (verify during build), the alternative is to have `BackTest` accept the brokerage directly in its constructor (it already holds `_portfolio` and `_benchmarkPortfolio`). In that case, add an `IBrokerage _brokerage` field to `BackTest` and call `_brokerage.SetBufferedMarketData(bufferedMarketData)` once.

#### 3.3 Update Tests

- `SimulatedBrokerageTests`: Add test for buffered path — verify inner fetcher is never called when buffer is set
- `BacktestTests`: Add test verifying `SetBufferedMarketData` is called on brokerage

---

### Phase 4: DI Wiring

#### 4.1 Create `CacheOptions`

Create `src/Application/Configuration/CacheOptions.cs`:

```csharp
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Directory for L2 CSV cache files. Null disables L2 caching.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Enable L1 in-process memory cache. Default: true.
    /// </summary>
    public bool EnableMemoryCache { get; set; } = true;
}
```

**Configuration example** (`appsettings.json`):

```json
{
  "Cache": {
    "DataDirectory": "./data/cache",
    "EnableMemoryCache": true
  }
}
```

**Default behavior when the `Cache` section is absent:** `CacheOptions` binds with default values — `DataDirectory` is `null` (L2 disabled), `EnableMemoryCache` is `true`. The DI factory in `ServiceCollectionExtensions` checks `DataDirectory`: when null, L2 decorators are skipped and only L1 in-memory caching is applied. When `EnableMemoryCache` is also set to `false`, no caching decorators are registered and fetchers pass through directly to the API.

#### 4.2 Update `ServiceCollectionExtensions`

Add cache decorator registration to `AddBoutquinTrading`:

```csharp
// After existing registrations:
services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

// Register IMarketDataFetcher with caching decorators
services.AddSingleton<IMarketDataFetcher>(sp =>
{
    var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;

    // Base: CompositeMarketDataFetcher (TiingoFetcher + FrankfurterFetcher)
    IMarketDataFetcher fetcher = sp.GetRequiredService<CompositeMarketDataFetcher>();

    // L2: Write-through CSV cache
    if (cacheOptions.DataDirectory != null)
    {
        fetcher = new WriteThroughMarketDataFetcher(
            fetcher,
            new CsvMarketDataFetcher(cacheOptions.DataDirectory),
            new CsvMarketDataStorage(cacheOptions.DataDirectory),
            cacheOptions.DataDirectory,
            sp.GetRequiredService<ILogger<WriteThroughMarketDataFetcher>>());
    }

    // L1: Memory cache
    if (cacheOptions.EnableMemoryCache)
    {
        fetcher = new CachingMarketDataFetcher(
            fetcher,
            sp.GetRequiredService<ILogger<CachingMarketDataFetcher>>());
    }

    return fetcher;
});

// Same pattern for IEconomicDataFetcher and IFactorDataFetcher
```

**Note:** The DI registration of `CompositeMarketDataFetcher`, `TiingoFetcher`, `FrankfurterFetcher` is NOT currently in `ServiceCollectionExtensions` (they're constructed manually). The DI wiring above assumes a future registration or manual construction at the call site. For now, the caching decorators work at any construction point — DI or manual.

---

## Composability Examples

### Minimal: L1 Only (No Disk Cache)

```csharp
var tiingo = new TiingoFetcher(httpClient, apiKey);
var frankfurter = new FrankfurterFetcher(httpClient);
var composite = new CompositeMarketDataFetcher(tiingo, frankfurter);
IMarketDataFetcher fetcher = new CachingMarketDataFetcher(composite);
```

### Full Stack: L1 + L2

```csharp
var tiingo = new TiingoFetcher(httpClient, apiKey);
var frankfurter = new FrankfurterFetcher(httpClient);
var composite = new CompositeMarketDataFetcher(tiingo, frankfurter);
var writeThroughFetcher = new WriteThroughMarketDataFetcher(
    composite, csvFetcher, csvStorage, dataDir, logger);
IMarketDataFetcher fetcher = new CachingMarketDataFetcher(writeThroughFetcher);
```

### ETFWealthIQ Pipeline: Pre-downloaded CSVs Only (Offline)

```csharp
// No API fetchers at all — read directly from CSVs downloaded by `download-data`
IMarketDataFetcher fetcher = new CachingMarketDataFetcher(
    new CsvMarketDataFetcher(dataDir));
```

---

## Testing Strategy

### Unit Tests (all phases)

| Test Class | Key Scenarios |
|-----------|---------------|
| `CachingMarketDataFetcherTests` | Cache hit, cache miss, thread safety, dispose, cancellation |
| `CachingEconomicDataFetcherTests` | Cache hit, miss, superset filter, date range normalization |
| `CachingFactorDataFetcherTests` | Cache hit, miss, daily vs monthly separation |
| `WriteThroughMarketDataFetcherTests` | L2 hit (CSV exists), L2 miss (write-through), partial cache, FX write |
| `WriteThroughEconomicDataFetcherTests` | L2 hit, L2 miss, round-trip verification |
| `WriteThroughFactorDataFetcherTests` | L2 hit, L2 miss, dataset/frequency variants |
| `SimulatedBrokerageTests` (update) | Buffered path eliminates fetch calls |
| `BacktestTests` (update) | `SetBufferedMarketData` called on brokerage |

### Mock Strategy

- Inner fetchers: `Moq.Mock<IMarketDataFetcher>` etc. — verify call counts
- CSV storage: Use temp directories (`Path.GetTempPath()`) — verify files created with correct format
- HTTP: No HTTP calls in any test — all mocked

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| L1 memory pressure for large universes | OOM for 500+ tickers × 20 years | L1 stores only what's been requested (not speculative). 40 tickers × 5800 days ≈ 50MB — within budget. Add `MaxMemoryCacheEntries` option if needed. |
| Stale L2 cache (CSV older than market close) | Backtest misses recent data | Freshness is the caller's responsibility (ETFWealthIQ's `--max-age-days` or `--force`). Engine does not manage TTL — historical data is immutable. |
| Write-through partial failure | CSV exists but incomplete | Write to temp file, atomic rename on success (same pattern as `ISortedStringTable` in Boutquin.Storage). Corrupt partial files are overwritten on next miss. |
| `SetBufferedMarketData` on `IBrokerage` — leaky abstraction | Interface gains a method only `SimulatedBrokerage` uses | Default interface method (C# 8+) means no impact on other implementations. Alternative: cast to `SimulatedBrokerage` inside `BackTest`. |
| L1 cache key collision (different date ranges for same symbols) | Returns wrong data | Cache key includes full parameter set. Superset-check only narrows, never widens. |

---

## Scope Boundaries

**In scope:**
- L1 memory cache decorators for all 3 fetcher interfaces
- L2 write-through decorators for all 3 fetcher interfaces
- FX rate CSV writing in `CsvMarketDataStorage`
- Backtest data prefetch (buffered `SimulatedBrokerage`)
- DI integration via `CacheOptions`
- `MarketDataFileNameHelper` extensions

**Out of scope:**
- `CsvEconomicDataFetcher`/`CsvEconomicDataStorage` creation (PL-02 deliverable)
- `CsvFactorDataFetcher`/`CsvFactorDataStorage` creation (PL-02 deliverable)
- Cache eviction policies (historical data is immutable; not needed)
- Distributed caching (Redis, etc. — single-machine workload)
- Bloom filters from Boutquin.Storage (insufficient ROI for 40-ticker universe)
- Database-backed cache (SQLite, PostgreSQL — CSV is sufficient for ~50MB)
- Automated cache refresh (ETFWealthIQ pipeline's `--max-age-days` handles this)
- `IMarketDataStorage` interface changes (FX write is a concrete method on `CsvMarketDataStorage`)

---

## Relationship to PL-02 (ETFWealthIQ Data Loader)

This spec and PL-02 are complementary:

| Concern | This Spec (Caching Architecture) | PL-02 (Data Loader) |
|---------|----------------------------------|---------------------|
| **What** | Transparent caching layer in the engine | CLI command to download data + CSV storage classes |
| **Where** | `Boutquin.Trading.Application` | `Boutquin.Trading.Domain` (CSV classes) + `ETFWealthIQ.Pipeline` (CLI) |
| **When** | Runtime — invisible to consumers | Pre-runtime — explicit user action |
| **CSV readers/writers** | Consumes (via write-through) | Produces (creates the classes) |

**Implementation order:**
1. This spec Phase 1 (L1 memory cache) — no dependencies
2. PL-02 Phase 1 (CSV storage/fetcher classes) — no dependencies
3. This spec Phase 2 (L2 write-through) — depends on PL-02 Phase 1
4. This spec Phase 3 (backtest prefetch) — no dependencies
5. PL-02 Phase 2 (pipeline CLI) — consumes engine CSV classes
6. This spec Phase 4 (DI wiring) — depends on Phase 1-3

---

## Validation Commands

```bash
# Build with warnings-as-errors
cd ~/Developer/Boutquin.Trading && dotnet build --configuration Release

# Run all tests
cd ~/Developer/Boutquin.Trading && dotnet test --no-build --configuration Release

# Verify no new warnings
cd ~/Developer/Boutquin.Trading && dotnet build --configuration Release 2>&1 | grep -c "warning"
# Expected: 0

# Format check
cd ~/Developer/Boutquin.Trading && dotnet format --verify-no-changes

# Type check (TreatWarningsAsErrors in .csproj handles this via dotnet build)
```

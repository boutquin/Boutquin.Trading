# Feature: Risk-Free Rate in BackTest Tearsheet

## Metadata
- **Source:** ETFWealthIQ pipeline wiring gap — `BackTest.AnalyzePerformanceMetrics()` hardcodes `riskFreeRate = 0` for Sharpe, Sortino, and Alpha calculations
- **Created:** 2026-03-17
- **Status:** DRAFT

## Problem Statement

`BackTest.AnalyzePerformanceMetrics()` calls `dailyReturns.SharpeRatio()`, `.SortinoRatio()`, and `.Alpha()` without passing a risk-free rate, defaulting to 0%. The extension methods already support a `riskFreeRate` parameter — the issue is that `BackTest` has no way to receive one from the caller and thread it through.

This means:
- Sharpe ratios are inflated (excess return is measured against 0%, not actual treasury yields)
- Sortino ratios have the same bias
- Alpha is overstated (CAPM alpha against 0% risk-free rate ≠ alpha against actual Rf)
- Consumers (ETFWealthIQ Pipeline) cannot produce accurate Tearsheets without post-hoc correction via `with` expressions, which is fragile and defeats the purpose of the engine computing the Tearsheet

The daily risk-free rate for a 23-year backtest (2003-2026) ranges from ~0% to ~5.3% annualized. Using 0% when the 3-month treasury yields 5% overstates Sharpe by ~0.3-0.5 (material for investment decisions).

## Solution Summary

Add a `decimal dailyRiskFreeRate` parameter to `BackTest` and thread it through to all risk-free-rate-sensitive calculations in `AnalyzePerformanceMetrics()`. The change is backward-compatible: the default is `0m`, matching current behavior.

## Acceptance Criteria

- [ ] `BacktestOptions` has a `RiskFreeRate` property (annualized, default 0)
- [ ] `BackTest` constructor accepts an optional `decimal dailyRiskFreeRate` parameter (default 0m)
- [ ] `AnalyzePerformanceMetrics()` passes `dailyRiskFreeRate` to `SharpeRatio()`, `SortinoRatio()`, and `Alpha()`
- [ ] Existing tests pass unchanged (backward-compatible via default 0)
- [ ] New tests verify that non-zero risk-free rate produces different (lower) Sharpe/Sortino/Alpha values
- [ ] `dotnet build` succeeds with zero warnings
- [ ] `dotnet test` passes all tests

## Deliverables

| File | Action | Purpose |
|------|--------|---------|
| `src/Application/Configuration/BacktestOptions.cs` | Update | Add `RiskFreeRate` property |
| `src/Application/Backtest.cs` | Update | Accept and thread `dailyRiskFreeRate` |
| `src/Application/Configuration/ServiceCollectionExtensions.cs` | Update | Convert `RiskFreeRate` (annual) to daily when constructing `BackTest` |
| `tests/UnitTests/Application/BacktestTests.cs` | Update | Add tests for non-zero risk-free rate |

## Relevant Files

| File | Why |
|------|-----|
| `src/Application/Backtest.cs:171-228` | `AnalyzePerformanceMetrics()` — the 3 calls that need the rate threaded through |
| `src/Application/Configuration/BacktestOptions.cs` | Options class: add `RiskFreeRate` property here |
| `src/Domain/Extensions/DecimalArrayExtensions.cs:111-113` | `SharpeRatio(decimal riskFreeRate = 0m)` — already accepts the parameter |
| `src/Domain/Extensions/DecimalArrayExtensions.cs:164-166` | `SortinoRatio(decimal riskFreeRate = 0m)` — already accepts the parameter |
| `src/Domain/Extensions/DecimalArrayExtensions.cs:416-419` | `Alpha(decimal[] benchmarkReturns, decimal riskFreeRate = 0m)` — already accepts the parameter |
| `src/Application/Configuration/ServiceCollectionExtensions.cs` | DI factory — reads `BacktestOptions` to construct `BackTest` |

## Implementation Plan

### Step 1: Add `RiskFreeRate` to `BacktestOptions`

Update `src/Application/Configuration/BacktestOptions.cs`:

```csharp
/// <summary>
/// The annualized risk-free rate as a decimal (e.g., 0.05 for 5%).
/// Used for Sharpe ratio, Sortino ratio, and CAPM alpha calculations.
/// Default: 0 (backward-compatible with existing behavior).
/// </summary>
public decimal RiskFreeRate { get; set; } = 0m;
```

### Step 2: Add `dailyRiskFreeRate` field to `BackTest`

Update `src/Application/Backtest.cs`:

Add a private field:
```csharp
private readonly decimal _dailyRiskFreeRate;
```

Add a new constructor overload (preserve backward compatibility):
```csharp
public BackTest(
    IPortfolio portfolio,
    IPortfolio benchmarkPortfolio,
    IMarketDataFetcher marketDataFetcher,
    CurrencyCode baseCurrency,
    ILogger<BackTest> logger,
    decimal dailyRiskFreeRate)
    : this(portfolio, benchmarkPortfolio, marketDataFetcher, baseCurrency, logger)
{
    _dailyRiskFreeRate = dailyRiskFreeRate;
}
```

The existing 5-parameter constructor chains to the primary and leaves `_dailyRiskFreeRate = 0m` (default). The existing 4-parameter backward-compatible constructor is unchanged.

### Step 3: Thread rate through `AnalyzePerformanceMetrics()`

Update lines 183, 184, and 189 in `AnalyzePerformanceMetrics()`:

```csharp
// Before:
var sharpeRatio = dailyReturns.SharpeRatio();
var sortinoRatio = dailyReturns.SortinoRatio();
var alpha = dailyReturns.Alpha(benchmarkDailyReturns);

// After:
var sharpeRatio = dailyReturns.SharpeRatio(_dailyRiskFreeRate);
var sortinoRatio = dailyReturns.SortinoRatio(_dailyRiskFreeRate);
var alpha = dailyReturns.Alpha(benchmarkDailyReturns, _dailyRiskFreeRate);
```

### Step 4: Update DI factory

In `ServiceCollectionExtensions.AddBoutquinTrading()`, if `BacktestOptions` is used to construct `BackTest`, convert the annual rate to daily:

```csharp
var dailyRfr = backtestOptions.RiskFreeRate / 252m;
```

**Note:** The current `ServiceCollectionExtensions` may not directly construct `BackTest` (the Sample program constructs it manually). In that case, document the conversion formula for consumers. ETFWealthIQ's `BlueprintConfigLoader` will read the rate from FRED data and convert.

### Step 5: Add tests

Add to existing backtest test file (or create new test class):

```csharp
[Fact]
public async Task RunAsync_WithNonZeroRiskFreeRate_ProducesLowerSharpeRatio()
{
    // Arrange: same portfolio/benchmark as existing tests
    var rfr = 0.05m / 252m; // 5% annualized → daily
    var backtestWithRfr = new BackTest(portfolio, benchmark, fetcher, CurrencyCode.USD, logger, rfr);
    var backtestZeroRfr = new BackTest(portfolio, benchmark, fetcher, CurrencyCode.USD, logger);

    // Act
    var tearsheetWithRfr = await backtestWithRfr.RunAsync(start, end);
    var tearsheetZeroRfr = await backtestZeroRfr.RunAsync(start, end);

    // Assert
    tearsheetWithRfr.SharpeRatio.Should().BeLessThan(tearsheetZeroRfr.SharpeRatio);
    tearsheetWithRfr.SortinoRatio.Should().BeLessThan(tearsheetZeroRfr.SortinoRatio);
}
```

## What Does NOT Need to Change

| Component | Why No Change |
|-----------|---------------|
| `DecimalArrayExtensions` methods | Already accept `riskFreeRate` parameter — no modification needed |
| `InformationRatio` | Measures active return vs benchmark, not vs risk-free rate — industry standard does not subtract Rf |
| `Beta` | Covariance/variance ratio — Rf cancels out in the CAPM derivation |
| `CalmarRatio`, `OmegaRatio`, `WinRate`, etc. | Not risk-free-rate dependent |
| `Tearsheet` record | Immutable record — no structural change. Values flow through unchanged. |
| `BenchmarkComparisonReport` | Receives pre-computed Tearsheet — no change needed |
| Factor regression (Alpha/Beta from Fama-French) | This is a separate concern. The pipeline overrides Tearsheet Alpha/Beta via `with` expression after running `FactorRegressor.Regress()`. No engine change needed for multi-factor alpha. |

## Scope Boundaries

**In scope:**
- `BacktestOptions.RiskFreeRate` property
- `BackTest` constructor + `AnalyzePerformanceMetrics()` threading
- Unit tests for non-zero rate

**Out of scope:**
- Time-varying risk-free rate (using a different Rf for each date in the backtest). This would require a `Func<DateOnly, decimal>` or a rate curve, which is a much larger change. For ETFWealthIQ, using the average 3-month treasury yield over the backtest period is sufficient.
- Fama-French multi-factor Alpha/Beta in `BackTest` — the pipeline handles this via `FactorRegressor` + `with` expression. No engine change needed.
- Yield curve construction — not needed by any engine analytics.

## Notes

- The convention in this engine is raw decimals, not percentages (`CLAUDE.md` convention). So `RiskFreeRate = 0.05m` means 5%, and the daily rate is `0.05m / 252m ≈ 0.000198m`.
- The ETFWealthIQ pipeline will fetch FRED series DGS3MO, compute the average over the backtest period, and pass it as `BacktestOptions.RiskFreeRate`.
- For backtests spanning periods with very different rate environments (2003-2026 spans 0% to 5%+), using a single average rate is an approximation. A time-varying rate would be more accurate but is significantly more complex. Document this limitation in the ETFWealthIQ rationale files.

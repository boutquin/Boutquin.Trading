# Boutquin.Trading — Competitive Analysis & Best-in-Class Roadmap

> **Date:** 2026-03-16
> **Scope:** Architecture review, competitive benchmarking, and phased improvement plan with TDD acceptance criteria

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State Assessment](#2-current-state-assessment)
3. [Competitive Landscape](#3-competitive-landscape)
4. [Feature Gap Analysis](#4-feature-gap-analysis)
5. [.NET Code Quality Assessment](#5-net-code-quality-assessment)
6. [Architecture Improvement Recommendations](#6-architecture-improvement-recommendations)
7. [Phased Roadmap](#7-phased-roadmap)
8. [Project-Level Acceptance Criteria & Quality Gates](#8-project-level-acceptance-criteria--quality-gates)

---

## 1. Executive Summary

Boutquin.Trading is a pre-release C# .NET 10 quantitative trading framework with a clean event-driven architecture, strong domain modeling, and solid financial calculation foundations. However, compared to industry leaders like **QuantConnect/Lean** (16k+ GitHub stars, 180+ contributors), **StockSharp** (90+ broker integrations), and Python powerhouses like **VectorBT** (vectorized performance) and **Backtrader** (extensive ecosystem), significant gaps exist in:

- **Strategy variety & API ergonomics** — Only BuyAndHold / RebalancingBuyAndHold strategies exist
- **Performance & scalability** — No vectorized computation path, no parallelization
- **Risk management** — No dedicated risk management module
- **Data pipeline breadth** — Limited to Tiingo equities + Frankfurter FX
- **Live trading** — No real broker integration
- **Analytics & visualization** — Console-only tearsheet output
- **Community & ecosystem** — Single contributor, no plugin architecture

This document proposes a **4-phase roadmap** to reach best-in-class status, with TDD acceptance criteria and quality gates for each phase.

---

## 2. Current State Assessment

### 2.1 Project Structure (13 projects)

| Project | Purpose | Maturity |
|---------|---------|----------|
| `Boutquin.Trading.Domain` | Core domain: interfaces, events, value objects, extensions | Solid |
| `Boutquin.Trading.Application` | Backtest engine, portfolio, strategies, event handlers | Functional |
| `Boutquin.Trading.Data.Tiingo` | Equity data fetcher (Tiingo API) | Functional |
| `Boutquin.Trading.Data.Frankfurter` | FX rate fetcher (Frankfurter API) | Functional |
| `Boutquin.Trading.Data.CSV` | CSV data reader | Basic |
| `Boutquin.Trading.Data.Processor` | Data processing pipeline | Basic |
| `Boutquin.Trading.DataAccess` | EF Core data access layer | Scaffolded |
| `Boutquin.Trading.BackTest` | Backtest runner entry point | Basic |
| `Boutquin.Trading.BenchMark` | Performance benchmarks | Basic |
| `Boutquin.Trading.Sample` | Sample usage | Minimal |
| `Boutquin.Trading.UnitTests` | Unit tests (xUnit) | Moderate coverage |
| `Tests/ArchitectureTests` | Architecture fitness functions | Basic |
| Solution-level | Directory.Build.props, .editorconfig, global.json | Well-configured |

### 2.2 Architecture Highlights

**Strengths:**
- **Event-driven pipeline**: `MarketEvent → SignalEvent → OrderEvent → FillEvent` — clean separation of concerns
- **Strategy pattern** via `IStrategy` with pluggable `IPositionSizer` and `IOrderPriceCalculationStrategy`
- **Multi-currency support** with FX conversion built into the core domain
- **Composite data fetcher pattern** (`CompositeMarketDataFetcher`)
- **Strong domain model** with value objects (`Asset`, `SecurityId`, `StrategyName`), enums, and guard clauses
- **Financial calculations**: Sharpe, Sortino, Alpha, Beta, Information Ratio, CAGR, Max Drawdown, Downside Deviation — all with proper sample-based divisors (N-1) and zero-denominator guards
- **Modern .NET**: .NET 10, C# 14, nullable reference types, `DateOnly`, primary constructors, `IAsyncEnumerable`, `record` types
- **Build quality**: Deterministic builds, SourceLink, MinVer versioning, TreatWarningsAsErrors

**Weaknesses:**
- **No dependency injection container** — components are wired manually
- **Mutable state on interfaces** — `IStrategy` exposes mutable `SortedDictionary<Asset, int> Positions` and `SortedDictionary<CurrencyCode, decimal> Cash` via interface
- **Business logic in interfaces** — `IStrategy.ComputeTotalValue()`, `UpdateCash()`, `UpdatePositions()` have default implementations with significant logic
- **No `IRiskManager`** interface or risk management pipeline stage
- **No universe selection** — assets are hardcoded at strategy creation
- **No indicator library** — strategies cannot compose technical indicators
- **No order book / Level 2 data support**
- **No streaming / real-time event bus** — synchronous event processing only
- **No serialization / state persistence** for resumable backtests
- **Limited slippage modeling** — fills at close price only for market orders

### 2.3 Performance Metrics (Tearsheet)

Currently supported: Annualized Return, Sharpe Ratio, Sortino Ratio, Max Drawdown, CAGR, Volatility, Alpha, Beta, Information Ratio, Equity Curve, Drawdowns, Max Drawdown Duration.

**Missing**: Calmar Ratio, Omega Ratio, Tail Ratio, Win/Loss Rate, Profit Factor, Average Win/Loss, Recovery Factor, VaR, CVaR, Skewness, Kurtosis, Monthly/Annual returns table.

---

## 3. Competitive Landscape

### 3.1 .NET Frameworks

#### QuantConnect/Lean
| Attribute | Detail |
|-----------|--------|
| **GitHub** | ~16.1k stars, ~4.3k forks, 180+ contributors |
| **Language** | C# core engine + Python 3.11 algorithms |
| **.NET Version** | .NET 6+ (currently migrating forward) |
| **Architecture** | Event-driven, modular plugin system (`IResultHandler`, `IRealtimeHandler`, `ISetupHandler`) |
| **Strategy Framework** | Alpha → Portfolio Construction → Execution → Risk Management pipeline |
| **Universe Selection** | Built-in with coarse/fine selection, ETF constituents, options chains |
| **Data** | 50+ data providers, alternative data, options, futures, crypto |
| **Brokers** | 20+ live broker integrations (IB, Alpaca, TradeStation, etc.) |
| **Risk Management** | Built-in risk management module with max drawdown, trailing stop, sector exposure limits |
| **Indicators** | 100+ built-in indicators (SMA, EMA, RSI, MACD, Bollinger, etc.) |
| **Performance** | Multi-threaded, cloud-optimized |
| **Testing** | Extensive CI/CD, thousands of unit tests |
| **What makes it best-in-class** | Complete end-to-end platform: research → backtest → paper trade → live trade, massive community, institutional adoption (300+ hedge funds) |

#### StockSharp (S#)
| Attribute | Detail |
|-----------|--------|
| **GitHub** | ~7k+ stars |
| **Language** | C# |
| **Architecture** | Component-based with Designer (visual), API, Shell, Hydra (data) |
| **Connections** | 90+ brokers/exchanges (crypto, equities, FX, options) |
| **Data** | High-compression storage (2 bytes/trade), candles, ticks, order books, options |
| **Strategy** | Visual designer + C# API + Python support |
| **What makes it best-in-class** | Broadest connectivity, visual strategy builder, HFT-capable with DMA |

#### TuringTrader
| Attribute | Detail |
|-----------|--------|
| **Language** | C# |
| **Focus** | End-of-day portfolio strategies |
| **Data** | Norgate, Tiingo, Yahoo, FRED |
| **What makes it best-in-class** | Clean API, production-proven with real capital |

### 3.2 Python Frameworks

#### Backtrader
| Attribute | Detail |
|-----------|--------|
| **GitHub** | ~14k+ stars |
| **Architecture** | Event-driven, cerebro engine |
| **Strengths** | Extremely extensible, custom indicators/analyzers/data feeds, live trading via IB/Oanda |
| **Weaknesses** | Steep learning curve, single-threaded, not actively maintained |
| **What makes it best-in-class** | Extensibility model — everything is pluggable |

#### VectorBT
| Attribute | Detail |
|-----------|--------|
| **GitHub** | ~4.5k+ stars |
| **Architecture** | Vectorized (NumPy/Numba), not event-driven |
| **Performance** | 1,000,000 orders in 70-100ms (M1), massive parameter sweeps |
| **Strengths** | Blazing fast, parameter optimization, interactive Plotly dashboards, ML integration |
| **Weaknesses** | PRO version is paid, limited live trading (via StrateQueue) |
| **What makes it best-in-class** | Performance — can test 20,000 parameter combinations in <30 seconds |

#### Zipline
| Attribute | Detail |
|-----------|--------|
| **GitHub** | ~18k+ stars (historical) |
| **Architecture** | Event-driven, pipeline API for factor research |
| **Status** | No longer actively maintained (Quantopian shut down); community fork `zipline-reloaded` exists |
| **What makes it best-in-class** | Pipeline API for factor-based strategies, integration with Alphalens/Pyfolio |

#### Backtesting.py
| Attribute | Detail |
|-----------|--------|
| **GitHub** | ~6k+ stars |
| **Architecture** | Lightweight, Pandas-based |
| **Strengths** | Beginner-friendly, clean API, interactive HTML reports (Bokeh) |
| **Weaknesses** | Limited features, no live trading |
| **What makes it best-in-class** | Fastest time from zero to first backtest |

### 3.3 Key Differentiators Summary

| Capability | Boutquin | Lean | StockSharp | Backtrader | VectorBT |
|-----------|----------|------|------------|------------|----------|
| **Language** | C# 14 / .NET 10 | C# / Python | C# | Python | Python |
| **Event-driven** | Yes | Yes | Yes | Yes | Hybrid |
| **Vectorized path** | No | No | No | No | **Yes** |
| **Universe selection** | No | **Yes** | Partial | No | No |
| **Built-in indicators** | No | **100+** | **Yes** | **130+** | **Yes** |
| **Risk management** | No | **Yes** | **Yes** | Partial | No |
| **Live broker** | No | **20+** | **90+** | IB/Oanda | Via adapter |
| **Multi-asset** | Equities + FX | **All** | **All** | All | All |
| **Options/Futures** | No | **Yes** | **Yes** | Via ext | No |
| **Visualization** | Console | **Web** | **Charts** | Matplotlib | **Plotly** |
| **Plugin architecture** | No | **Yes** | **Yes** | **Yes** | Partial |
| **DI container** | No | **Yes** | **Yes** | N/A | N/A |
| **State persistence** | No | **Yes** | **Yes** | No | No |
| **CI/CD** | GitHub Actions | **Full** | **Full** | Basic | Basic |
| **Test coverage** | Moderate | **High** | Moderate | Low | Moderate |
| **Community** | 1 contributor | **180+** | **Large** | **Large** | **Growing** |

---

## 4. Feature Gap Analysis

### 4.1 Critical Gaps (Must-have for a credible framework)

| # | Gap | Impact | Competitors with feature |
|---|-----|--------|--------------------------|
| G1 | No indicator library | Cannot build any technical strategy | Lean (100+), Backtrader (130+), StockSharp |
| G2 | No risk management module | Cannot control drawdown, exposure, position limits | Lean, StockSharp |
| G3 | Only 2 built-in strategies | Unusable for real strategy development | All competitors |
| G4 | No universe selection | Cannot rotate assets or filter dynamically | Lean |
| G5 | No DI container | Hard to configure, test, and extend | Lean, StockSharp |
| G6 | No plugin/extension architecture | Cannot add custom data sources or brokers without forking | Lean, StockSharp, Backtrader |
| G7 | Mutable state on interfaces | Thread-safety issues, breaks encapsulation | N/A (design issue) |

### 4.2 Important Gaps (Needed for competitive parity)

| # | Gap | Impact | Competitors with feature |
|---|-----|--------|--------------------------|
| G8 | No slippage model | Overly optimistic backtests | Lean, Backtrader, VectorBT |
| G9 | No transaction cost model beyond flat commission | Unrealistic cost estimation | Lean, Backtrader |
| G10 | Limited performance metrics | Missing Calmar, Omega, VaR, CVaR, win rate, etc. | Lean, VectorBT, Backtrader |
| G11 | No visualization | Cannot visually inspect results | All Python frameworks, StockSharp |
| G12 | No live broker integration | Backtest-only | Lean, StockSharp, Backtrader |
| G13 | No options/futures/crypto support | Equity-only limits market | Lean, StockSharp |
| G14 | No order book / Level 2 data | Cannot model market microstructure | StockSharp |

### 4.3 Nice-to-Have Gaps (For best-in-class status)

| # | Gap | Impact |
|---|-----|--------|
| G15 | No vectorized computation path | Slower parameter optimization |
| G16 | No walk-forward analysis / Monte Carlo simulation | Limited robustness testing |
| G17 | No ML integration pipeline | Cannot use modern ML-based strategies |
| G18 | No web-based UI / dashboard | Less accessible than Lean Cloud or VectorBT notebooks |
| G19 | No state persistence / resumable backtests | Cannot checkpoint long runs |
| G20 | No scheduling / real-time event bus | Cannot handle intraday or streaming data |

---

## 5. .NET Code Quality Assessment

### 5.1 What's Best-in-Class

| Aspect | Assessment | Details |
|--------|------------|---------|
| **.NET version** | Excellent | .NET 10 with C# 14 — ahead of most competitors |
| **Build configuration** | Excellent | Deterministic builds, SourceLink, MinVer, TreatWarningsAsErrors |
| **Value objects** | Good | `Asset`, `SecurityId`, `StrategyName` — proper DDD value objects |
| **Record types** | Good | `Tearsheet`, events use immutable records |
| **Guard clauses** | Good | Consistent use of `Guard.Against*` pattern |
| **Async/await** | Good | Proper `ConfigureAwait(false)` usage throughout |
| **IAsyncEnumerable** | Good | Data fetchers use async streams — modern pattern |
| **Nullable reference types** | Enabled | Though many warnings are suppressed in Directory.Build.props |

### 5.2 What Needs Improvement

| Issue | Severity | Details | Suggested Fix |
|-------|----------|---------|---------------|
| **I1: Business logic in interfaces** | High | `IStrategy` has 3 default method implementations (`ComputeTotalValue`, `UpdateCash`, `UpdatePositions`) with 70+ lines of logic. Interfaces should define contracts, not behavior. | Extract to abstract base class `StrategyBase` |
| **I2: Mutable collections on interface** | High | `IStrategy.Positions` and `IStrategy.Cash` expose mutable `SortedDictionary` — any consumer can mutate strategy state | Return `IReadOnlyDictionary` from interface; keep mutable backing in implementation |
| **I3: Suppressed nullable warnings** | Medium | `CS8600-CS8625` all suppressed in Directory.Build.props — defeats purpose of enabling nullable | Fix warnings incrementally; remove suppressions per-project as resolved |
| **I4: No dependency injection** | Medium | `Portfolio` directly `new`s `EventProcessor` in constructor (line 105) — tight coupling | Use `IServiceCollection` / `IServiceProvider` for composition |
| **I5: Event handler coupling** | Medium | `IBrokerage.FillOccurred` uses `Func<object, FillEvent, Task>` — custom event pattern. Should use `IObservable<T>` or a proper event bus | Introduce `IEventBus` or use System.Reactive |
| **I6: No CancellationToken support** | Medium | `RunAsync`, `FetchMarketDataAsync`, `SubmitOrderAsync` — none accept `CancellationToken` | Add optional `CancellationToken` parameter to all async methods |
| **I7: Magic numbers** | Low | `DefaultTradingDaysInYear = 252` is fine but `commissionRate = 0.001m` in `SimulatedBrokerage` is hidden | Make all magic numbers configurable or at minimum named constants |
| **I8: No logging** | Medium | No `ILogger<T>` usage anywhere — makes debugging impossible | Add `Microsoft.Extensions.Logging` throughout |
| **I9: No `IDisposable` / resource cleanup** | Low | `HttpClient` usage in data fetchers — lifetime management unclear | Use `IHttpClientFactory` |
| **I10: Large interface surface** | Medium | `IPortfolio` has 14 methods — too broad, violates ISP | Split into `IPortfolioReader`, `IPortfolioWriter`, `IPortfolioManager` |

### 5.3 Code Smell Summary

```
Critical:   I1 (logic in interfaces), I2 (mutable state on interfaces)
Important:  I3 (nullable), I4 (no DI), I5 (event coupling), I6 (no cancellation), I8 (no logging), I10 (ISP)
Minor:      I7 (magic numbers), I9 (disposable)
```

---

## 6. Architecture Improvement Recommendations

### 6.1 Proposed Target Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Boutquin.Trading.Host                        │
│                  (Console / Web API / Worker Service)                │
│                     Microsoft.Extensions.Hosting                    │
├─────────────────────────────────────────────────────────────────────┤
│                     Boutquin.Trading.Application                    │
│  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌──────────────────────┐  │
│  │ Backtest  │ │ Live      │ │ Paper    │ │ Strategy Orchestrator│  │
│  │ Engine    │ │ Engine    │ │ Trading  │ │ (Universe → Alpha →  │  │
│  │           │ │           │ │ Engine   │ │  Portfolio → Risk →  │  │
│  │           │ │           │ │          │ │  Execution)          │  │
│  └──────────┘ └───────────┘ └──────────┘ └──────────────────────┘  │
├─────────────────────────────────────────────────────────────────────┤
│                      Boutquin.Trading.Domain                        │
│  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐  │
│  │Indicators│ │ Risk      │ │ Events   │ │Portfolio │ │Analytics│  │
│  │ Library  │ │Management │ │ & Bus    │ │Construct.│ │& Report │  │
│  └──────────┘ └───────────┘ └──────────┘ └──────────┘ └────────┘  │
├─────────────────────────────────────────────────────────────────────┤
│                   Infrastructure / Data Layer                       │
│  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐  │
│  │ Tiingo   │ │Frankfurter│ │ CSV      │ │ IB       │ │ Alpaca │  │
│  │ Fetcher  │ │ Fetcher   │ │ Fetcher  │ │ Broker   │ │ Broker │  │
│  └──────────┘ └───────────┘ └──────────┘ └──────────┘ └────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.2 Key Architectural Changes

| Change | Rationale | Inspired By |
|--------|-----------|-------------|
| **A1: Strategy Framework Pipeline** | Replace single `GenerateSignals` → adopt `IUniverseSelector → IAlphaModel → IPortfolioConstructionModel → IRiskModel → IExecutionModel` | QuantConnect/Lean Algorithm Framework |
| **A2: Indicator Library** | Composable indicator system with `IIndicator<T>`, rolling windows, warm-up periods | Lean indicators + Backtrader indicators |
| **A3: Risk Management Module** | `IRiskManager` with pluggable rules: max drawdown, max position size, sector exposure, VAR limits | Lean Risk Management |
| **A4: Event Bus / Mediator** | Replace direct event coupling with `IEventBus` (pub/sub) using `MediatR` or custom implementation | StockSharp, general CQRS pattern |
| **A5: Plugin Architecture** | `IDataProvider`, `IBrokeragePlugin` with assembly scanning and DI registration | Lean modular design |
| **A6: Dependency Injection** | `Microsoft.Extensions.DependencyInjection` throughout, hosted service pattern | .NET best practice |
| **A7: Configuration System** | `IOptions<T>` pattern for strategy parameters, commission models, data sources | .NET best practice |
| **A8: Logging & Observability** | `ILogger<T>`, structured logging, OpenTelemetry metrics for backtest performance | .NET best practice |
| **A9: Analytics Engine** | Extended metrics (Calmar, Omega, VaR, CVaR, win/loss, Profit Factor), HTML report generation | VectorBT analytics, Pyfolio |
| **A10: Vectorized Fast Path** | Optional `Span<T>` / SIMD-based computation path for parameter sweeps | VectorBT approach, adapted for .NET |

---

## 7. Phased Roadmap

### Phase 1: Foundation Hardening (Weeks 1-4)
**Theme:** Fix architectural issues, establish best-in-class .NET patterns

#### 7.1.1 Tasks

| ID | Task | TDD Acceptance Criteria |
|----|------|------------------------|
| P1-01 | **Extract `StrategyBase` abstract class** from `IStrategy` default implementations | Tests: `StrategyBase.ComputeTotalValue()` returns correct value for multi-currency portfolio; `UpdateCash()` adds to existing balance; `UpdatePositions()` increments quantity; all existing `IStrategy` tests pass unchanged |
| P1-02 | **Make interface collections immutable** — `IStrategy.Positions` → `IReadOnlyDictionary`, `IStrategy.Cash` → `IReadOnlyDictionary` | Tests: Verify consumers cannot cast to mutable; verify `StrategyBase` internal mutation still works; verify serialization round-trip |
| P1-03 | **Add `CancellationToken` to all async APIs** | Tests: Cancellation propagates through `BackTest.RunAsync`, `FetchMarketDataAsync`, `SubmitOrderAsync`; cancelled backtest throws `OperationCanceledException` |
| P1-04 | **Introduce DI container** (`Microsoft.Extensions.DependencyInjection`) | Tests: All services resolve correctly; `Portfolio` receives `IEventProcessor` via constructor; `BackTest` receives dependencies via DI |
| P1-05 | **Add `ILogger<T>` throughout** | Tests: Verify log output for key events (backtest start/end, order filled, signal generated); verify no exceptions when logger is NullLogger |
| P1-06 | **Split `IPortfolio`** into `IPortfolioReader` + `IPortfolioCommands` | Tests: Read-only consumers compile with `IPortfolioReader`; command consumers use `IPortfolioCommands`; existing tests pass |
| P1-07 | **Fix nullable warnings** — remove CS8600-CS8625 suppressions from `Directory.Build.props` | Tests: Clean build with zero nullable warnings; no runtime `NullReferenceException` in existing test suite |
| P1-08 | **Add `IHttpClientFactory`** for data fetchers | Tests: Multiple concurrent `FetchMarketDataAsync` calls don't exhaust sockets; mock handler works in tests |
| P1-09 | **Introduce `IOptions<BacktestConfiguration>`** for configurable parameters | Tests: `TradingDaysPerYear`, `CommissionRate`, `SlippageModel` are configurable via `appsettings.json`; defaults match current hardcoded values |

#### 7.1.2 Quality Gate: Phase 1

| Gate | Criteria |
|------|----------|
| **Build** | Zero warnings, zero nullable warnings, deterministic build passes |
| **Tests** | 100% existing tests pass + new tests for each P1 task; ≥80% line coverage on Domain + Application |
| **Architecture** | ArchitectureTests verify: no mutable collections on interfaces, no `new` of infrastructure types in Domain, no direct `HttpClient` instantiation |
| **Code Review** | All changes reviewed against .NET coding conventions |

---

### Phase 2: Strategy & Analytics (Weeks 5-10)
**Theme:** Build the indicator library, risk management, and analytics engine — reach feature parity with simpler competitors

#### 7.2.1 Tasks

| ID | Task | TDD Acceptance Criteria |
|----|------|------------------------|
| P2-01 | **Indicator framework** — `IIndicator<T>`, `IndicatorBase<T>`, rolling window, warm-up period tracking | Tests: SMA(10) produces correct values for known dataset; EMA warm-up period = N; indicator `IsReady` is false until warm-up complete |
| P2-02 | **Core indicators** — SMA, EMA, RSI, MACD, Bollinger Bands, ATR, Stochastic, OBV, VWAP | Tests: Each indicator validated against known reference values (e.g., from TA-Lib or Investopedia examples); edge cases (single data point, all same values, extreme values) |
| P2-03 | **Composite indicators** — Indicator-of-indicator pattern (e.g., RSI of SMA) | Tests: `RSI(SMA(close, 10), 14)` produces correct values; chain of 3+ indicators works |
| P2-04 | **Risk Management module** — `IRiskManager` with rules: `MaxDrawdownRule`, `MaxPositionSizeRule`, `MaxExposureRule` | Tests: Order rejected when drawdown exceeds 20%; position size capped at 5% of portfolio; total exposure capped at configured limit |
| P2-05 | **Strategy framework pipeline** — `IAlphaModel → IPortfolioConstructionModel → IRiskModel → IExecutionModel` | Tests: Signal flows through full pipeline; risk model can veto orders; execution model can split large orders |
| P2-06 | **Slippage model** — `ISlippageModel` with `FixedSlippage`, `VolumeShareSlippage`, `ConstantPercentageSlippage` | Tests: Market order fill price adjusted by slippage; slippage increases with order size for `VolumeShareSlippage` |
| P2-07 | **Transaction cost model** — `ITransactionCostModel` with `PerShareCommission`, `PercentageCommission`, `TieredCommission` | Tests: Commission calculated correctly for each model; tiered model transitions at breakpoints |
| P2-08 | **Extended analytics** — Calmar Ratio, Omega Ratio, Tail Ratio, VaR (historical & parametric), CVaR, Win Rate, Profit Factor, Recovery Factor, Skewness, Kurtosis | Tests: Each metric validated against known reference values; edge cases (no trades, all winning, all losing) |
| P2-09 | **Monthly / Annual returns table** | Tests: Returns table matches manual calculation for sample equity curve |
| P2-10 | **HTML report generator** — Tearsheet to interactive HTML with equity curve chart, drawdown chart, monthly returns heatmap | Tests: Generated HTML is valid; contains expected charts; file size < 2MB for 5-year backtest |

#### 7.2.2 Quality Gate: Phase 2

| Gate | Criteria |
|------|----------|
| **Build** | Clean build, all Phase 1 gates still pass |
| **Tests** | ≥90% line coverage on indicator library; all indicator tests pass against reference values (maximum 0.001% deviation) |
| **Benchmarks** | SMA(200) on 10,000 data points < 1ms; full indicator suite warm-up < 50ms |
| **Architecture** | Indicators are stateless computations or self-contained stateful objects; no indicator depends on `IPortfolio` or `IStrategy` directly |
| **Documentation** | Each indicator has XML doc with formula, example, and reference link |

---

### Phase 3: Ecosystem & Live Trading (Weeks 11-18)
**Theme:** Plugin architecture, broker integration, universe selection — reach parity with Lean/StockSharp

#### 7.3.1 Tasks

| ID | Task | TDD Acceptance Criteria |
|----|------|------------------------|
| P3-01 | **Plugin architecture** — `IDataProviderPlugin`, `IBrokeragePlugin` with assembly scanning | Tests: Plugin discovered from external assembly; plugin registered in DI; plugin lifecycle (init, start, stop) works correctly |
| P3-02 | **Universe selection** — `IUniverseSelector` with `ManualUniverse`, `TopByVolumeUniverse`, `ETFConstituentsUniverse` | Tests: Universe changes trigger rebalance; removed assets have positions closed; new assets eligible for signals |
| P3-03 | **Interactive Brokers integration** — `IBBrokerage : IBrokerage` | Tests: Mock IB gateway accepts market order; fill event received; position update reflected in portfolio |
| P3-04 | **Alpaca integration** — `AlpacaBrokerage : IBrokerage` | Tests: Paper trading order submission; market data streaming; position sync |
| P3-05 | **Event bus / Mediator** — Replace direct event coupling with `IEventBus` | Tests: Event published → all subscribers notified; unsubscribed handler not called; async handlers complete before next event |
| P3-06 | **State persistence** — Checkpoint/resume for long backtests | Tests: Backtest interrupted at day 100 → resumed from checkpoint → results identical to uninterrupted run |
| P3-07 | **Crypto data source** — Binance/CCXT data fetcher | Tests: Fetch BTC/USDT OHLCV data; multiple timeframes; rate limiting respected |
| P3-08 | **Options data support** — `OptionContract` entity, `IOptionChainProvider` | Tests: Option chain for AAPL retrieved; Greeks calculated (Black-Scholes); option strategy (covered call) backtested |
| P3-09 | **Scheduling & real-time** — `IScheduler` for intraday events, market open/close, timer-based triggers | Tests: Timer fires at specified interval; market open event fires at 9:30 ET; schedule survives DST transitions |
| P3-10 | **Paper trading mode** — Unified engine for simulated live trading with real market data | Tests: Paper trade executes same strategy as backtest; fills use real-time data; portfolio state persists across sessions |

#### 7.3.2 Quality Gate: Phase 3

| Gate | Criteria |
|------|----------|
| **Build** | All previous gates pass |
| **Integration Tests** | IB and Alpaca paper trading tests pass (may require test accounts) |
| **Plugin Contract** | Any `IDataProviderPlugin` implementation can be loaded from a separate NuGet package |
| **Performance** | Event bus throughput ≥ 100,000 events/second |
| **Security** | API keys stored via `IConfiguration` + user secrets, never in code; all HTTP calls use HTTPS |
| **Documentation** | Plugin development guide with sample plugin project |

---

### Phase 4: Performance & Polish (Weeks 19-24)
**Theme:** Vectorized computation, ML integration, community readiness — best-in-class differentiation

#### 7.4.1 Tasks

| ID | Task | TDD Acceptance Criteria |
|----|------|------------------------|
| P4-01 | **Vectorized backtest engine** — `Span<T>` / `Vector<T>` SIMD path for parameter sweeps | Tests: Vectorized SMA matches scalar SMA for all test cases; 1000-parameter sweep completes in <5 seconds for 5-year daily data |
| P4-02 | **Walk-forward optimization** — `IOptimizer` with `WalkForwardOptimizer`, `GridSearchOptimizer` | Tests: Walk-forward produces out-of-sample results; grid search explores full parameter space; optimal parameters differ from in-sample |
| P4-03 | **Monte Carlo simulation** — Bootstrap resampling of trades/returns | Tests: 1000 simulations produce distribution of Sharpe ratios; confidence intervals calculated; p-value for strategy significance |
| P4-04 | **ML integration pipeline** — `IFeatureExtractor`, `IModelTrainer`, `IPredictor` with scikit-learn-style API | Tests: Feature matrix generated from market data; model trained on in-sample; predictions used as alpha signals |
| P4-05 | **Web dashboard** — Blazor or minimal API endpoint serving backtest results | Tests: Dashboard loads in <2 seconds; equity curve chart renders; live backtest progress updates via SignalR |
| P4-06 | **NuGet packaging** — Publish core libraries as NuGet packages | Tests: `dotnet add package Boutquin.Trading.Domain` works; version number correct; dependencies resolved |
| P4-07 | **Sample strategies** — Mean Reversion, Momentum, Pairs Trading, Factor-based | Tests: Each strategy produces expected behavior on known test data; tearsheet metrics within expected ranges |
| P4-08 | **Performance benchmarks** — BenchmarkDotNet suite for critical paths | Tests: No regression > 10% from baseline; memory allocation within budget |
| P4-09 | **API documentation** — DocFX or similar API doc generation | Tests: All public types have XML docs; generated site builds without errors |
| P4-10 | **Contributing guide & architecture decision records** | Tests: New contributor can clone, build, test, and run sample in <15 minutes |

#### 7.4.2 Quality Gate: Phase 4

| Gate | Criteria |
|------|----------|
| **Build** | All previous gates pass |
| **Performance** | Vectorized path ≥ 10x faster than scalar for parameter sweeps |
| **NuGet** | Packages published to NuGet.org (or private feed) with correct metadata |
| **Documentation** | API docs deployed; README updated with badges, quick-start guide |
| **Community** | CONTRIBUTING.md, CODE_OF_CONDUCT.md, issue templates, PR templates in place |
| **Samples** | All sample strategies run successfully against CSV test data included in repo |

---

## 8. Project-Level Acceptance Criteria & Quality Gates

### 8.1 Overall Project Quality Gates

These gates apply across all phases and must be maintained continuously:

| Gate ID | Gate | Criteria | Verification |
|---------|------|----------|-------------|
| **QG-01** | **Build Health** | Zero errors, zero warnings (TreatWarningsAsErrors already enabled), deterministic builds | `dotnet build` in CI |
| **QG-02** | **Test Coverage** | ≥85% line coverage overall; ≥95% on Domain; ≥90% on Application | Coverlet + ReportGenerator in CI |
| **QG-03** | **Architecture Fitness** | No circular dependencies between projects; Domain has zero infrastructure references; all interfaces in Domain | NetArchTest or ArchUnitNET in Architecture Tests project |
| **QG-04** | **Performance Budget** | No single backtest day processing > 10ms; full 5-year daily backtest < 30 seconds (single asset) | BenchmarkDotNet |
| **QG-05** | **Security** | No secrets in code; all HTTP via HTTPS; input validation on all public APIs | Static analysis + manual review |
| **QG-06** | **Nullable Safety** | Zero nullable warnings (suppressions progressively removed) | `dotnet build` with suppressions removed |
| **QG-07** | **API Stability** | No breaking changes to public API without major version bump; `[Obsolete]` before removal | API diff tool in CI |
| **QG-08** | **Documentation** | All public types and members have XML documentation | `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + CI check |
| **QG-09** | **Dependency Hygiene** | No vulnerable NuGet packages; dependencies reviewed quarterly | `dotnet list package --vulnerable` in CI |
| **QG-10** | **Continuous Integration** | All gates enforced in CI pipeline; PR cannot merge with failing gates | GitHub Actions workflow |

### 8.2 TDD Process Requirements

All new code must follow Red-Green-Refactor:

1. **Red**: Write a failing test that defines the expected behavior
2. **Green**: Write the minimum code to make the test pass
3. **Refactor**: Improve code structure while keeping tests green

**Test naming convention:** `MethodName_Scenario_ExpectedBehavior`
**Test organization:** Mirror source project structure in test project
**Test data:** Use `[Theory]` with `[MemberData]` or `[ClassData]` for parameterized tests (already established pattern in `DecimalArrayExtensionsTestData`)

### 8.3 Definition of Done (Per Task)

- [ ] All acceptance criteria tests written and passing
- [ ] No new warnings introduced
- [ ] XML documentation on all new public members
- [ ] Existing tests unbroken
- [ ] Architecture tests pass
- [ ] Code reviewed
- [ ] Benchmark run (if performance-sensitive)

### 8.4 Phase Completion Criteria

| Phase | Complete When |
|-------|--------------|
| **Phase 1** | All P1 tasks done; QG-01 through QG-06 green; existing functionality preserved |
| **Phase 2** | All P2 tasks done; indicator library usable for SMA crossover strategy end-to-end; HTML report generated |
| **Phase 3** | All P3 tasks done; at least one live broker (paper mode) tested; plugin loaded from external assembly |
| **Phase 4** | All P4 tasks done; NuGet packages published; sample strategies runnable; documentation deployed |

### 8.5 Best-in-Class Milestone Criteria

Boutquin.Trading can claim **best-in-class .NET backtesting framework** status when:

1. **Feature parity with Lean core** — Strategy pipeline, indicators, risk management, multi-asset, universe selection
2. **Superior .NET quality** — Latest .NET version, modern C# idioms, full nullable safety, DI-based composition, OpenTelemetry observability
3. **Performance competitive with VectorBT** — Vectorized parameter sweeps within 5x of VectorBT for equivalent workloads (accounting for language differences)
4. **At least 2 live broker integrations** — Interactive Brokers + one other (Alpaca recommended)
5. **Community readiness** — NuGet packages, API docs, contributing guide, sample strategies, issue templates
6. **Test coverage ≥ 90%** with architecture fitness functions enforcing design constraints
7. **At least 20 built-in indicators** validated against reference implementations
8. **HTML tearsheet report** with interactive charts, matching or exceeding Pyfolio/QuantStats output quality

---

## Appendix A: Sources & References

- [QuantConnect/Lean — GitHub](https://github.com/QuantConnect/Lean) (~16.1k stars, 180+ contributors)
- [QuantConnect Documentation](https://www.quantconnect.com/docs/v2/lean-engine/getting-started)
- [StockSharp — GitHub](https://github.com/StockSharp/StockSharp) (~7k+ stars, 90+ broker integrations)
- [StockSharp Documentation](https://doc.stocksharp.com/)
- [VectorBT — GitHub](https://github.com/polakowo/vectorbt) (~4.5k stars)
- [VectorBT Features](https://vectorbt.dev/getting-started/features/)
- [Backtrader — GitHub](https://github.com/mementum/backtrader) (~14k+ stars)
- [Backtesting.py](https://kernc.github.io/backtesting.py/)
- [TuringTrader](https://www.turingtrader.org/)
- [QuantStart — Backtesting Frameworks](https://www.quantstart.com/articles/backtesting-systematic-trading-strategies-in-python-considerations-and-open-source-frameworks/)

## Appendix B: Glossary

| Term | Definition |
|------|-----------|
| **Alpha Model** | Component that generates expected return signals for assets |
| **Universe Selection** | Dynamic filtering of tradeable assets |
| **Portfolio Construction** | Translating alpha signals into target portfolio weights |
| **Risk Model** | Rules that constrain or reject orders based on risk limits |
| **Execution Model** | Algorithm for converting target orders into actual market orders (e.g., TWAP, VWAP) |
| **Tearsheet** | Summary report of strategy performance metrics |
| **Walk-Forward** | Out-of-sample testing by rolling the optimization window forward |
| **VaR** | Value at Risk — maximum expected loss at a given confidence level |
| **CVaR** | Conditional Value at Risk — expected loss beyond VaR threshold |

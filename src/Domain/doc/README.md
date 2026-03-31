# Boutquin.Trading.Domain

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.domain?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Domain Layer

Core domain contracts, events, value objects, enums, and extension methods for the [Boutquin.Trading](https://github.com/boutquin/Boutquin.Trading) quantitative trading framework.

This package defines all abstractions — Application and Data layer packages provide implementations.

### Interfaces (33)

| Category | Interfaces |
|----------|------------|
| Core Engine | `IPortfolio`, `IBrokerage`, `IStrategy`, `IPositionSizer`, `IEventProcessor`, `IEventHandler`, `IFinancialEvent` |
| Construction | `IPortfolioConstructionModel`, `IRobustConstructionModel`, `ILeveragedConstructionModel`, `ICovarianceEstimator`, `IDownsideRiskMeasure`, `IRebalancingTrigger` |
| Risk | `IRiskManager`, `IRiskRule`, `IDrawdownControl` |
| Tactical | `IIndicator`, `IMacroIndicator`, `IRegimeClassifier` |
| Universe | `IUniverseSelector`, `ITimedUniverseSelector` |
| Data | `IMarketDataFetcher`, `IMarketDataStorage`, `IMarketDataProcessor`, `IEconomicDataFetcher`, `IFactorDataFetcher`, `ICurrencyConversionService`, `ISymbolReader` |
| Infrastructure | `ITradingCalendar`, `ITransactionCostModel`, `ISlippageModel`, `IOrderPriceCalculationStrategy`, `ICapitalAllocationStrategy` |

### Events
`MarketEvent`, `SignalEvent`, `OrderEvent`, `FillEvent` — the four-stage event pipeline driving the backtest engine.

### Enums (14)
`AssetClassCode`, `CalendarCompositionMode`, `ContinentCode`, `CountryCode`, `CurrencyCode`, `EconomicRegime`, `ExchangeCode`, `FamaFrenchDataset`, `OrderType`, `RebalancingFrequency`, `SecuritySymbolStandard`, `SignalType`, `TimeZoneCode`, `TradeAction`

### Value Objects
`Asset`, `SecurityId`, `StrategyName`, `RiskEvaluation`, `BatchRiskEvaluation`, `AssetWeightConstraints`

### Analytics Records
`BrinsonFachlerResult`, `CorrelationAnalysisResult`, `DrawdownPeriod`, `FactorRegressionResult`, `WalkForwardResult`, `MonteCarloResult`, `AssetMetadata`

### Extension Methods
- `DecimalArrayExtensions` — 20+ financial metrics (Sharpe, Sortino, Calmar, MaxDrawdown, VaR, CVaR, etc.) on `decimal[]`
- `EquityCurveExtensions` — Drawdown analysis, monthly/annual returns from equity curves

### Helpers
`RollingWindow<T>` (generic circular buffer), `TearSheet` (performance summary record), `FamaFrenchConstants`, `MarketDataFileNameHelper`, `CholeskyQpSolver`

## Installation

```sh
dotnet add package Boutquin.Trading.Domain
```

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.

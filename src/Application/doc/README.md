# Boutquin.Trading.Application

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.application?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Application Layer

The core application layer for a multi-asset, multi-currency, multi-strategy, event-driven trading platform. Provides backtesting, portfolio construction, analytics, risk management, caching, and DI registration.

### Key Components

| Component | Description |
|-----------|-------------|
| `Portfolio` | Multi-currency cash management, position tracking, equity curve computation |
| `BackTest` | Event-driven backtesting engine with market data prefetch and structured logging |
| `SimulatedBrokerage` | Market, limit, stop, and stop-limit order execution with slippage and commission; logs warnings on dropped orders due to missing market data |
| DRIP | Optional automatic dividend reinvestment into whole shares at Close price (`EnableDividendReinvestment`) |
| Expense Ratio | Configurable annual expense ratio in basis points, deducted daily before equity curve update (`AnnualExpenseRatioBps`) |

### Strategies
- `BuyAndHoldStrategy` — Static allocation, no rebalancing
- `RebalancingBuyAndHoldStrategy` — Periodic rebalancing to target weights
- `ConstructionModelStrategy` — Dynamic weights from portfolio construction models with rolling returns

### Portfolio Construction (18 Models + 1 Decorator)

| Model | Algorithm |
|-------|-----------|
| `EqualWeightConstruction` | Uniform 1/N allocation |
| `InverseVolatilityConstruction` | Weight inversely proportional to realized volatility |
| `MinimumVarianceConstruction` | Projected gradient descent minimizing portfolio variance |
| `MeanVarianceConstruction` | Projected gradient descent maximizing Sharpe ratio |
| `RiskParityConstruction` | Iterative inverse-MRC equalization |
| `MaximumDiversificationConstruction` | Maximize diversification ratio (Chopin & Briand, 2008) |
| `HierarchicalRiskParityConstruction` | Lopez de Prado (2016) clustering + recursive bisection |
| `HierarchicalEqualRiskContributionConstruction` | Cluster-based equal risk contribution |
| `ReturnTiltedHrpConstruction` | Lohre, Rother, Schafer (2020) HRP with softmax return signal (active in all market regimes) |
| `BlackLittermanConstruction` | Bayesian equilibrium + investor views; no-views case returns equilibrium weights directly |
| `DynamicBlackLittermanConstruction` | Time-varying views with adaptive confidence; omega clamped to prevent singularity at confidence=1.0 |
| `MeanDownsideRiskConstruction` | Pluggable downside risk: CVaR or Sortino via `IDownsideRiskMeasure` |
| `RobustMeanVarianceConstruction` | Minimax across covariance scenarios (regime-resilient) |
| `TacticalOverlayConstruction` | Regime-specific tilts + momentum scoring |
| `VolatilityTargetingConstruction` | Scale weights to target portfolio vol, capped leverage |
| `WeightConstrainedConstruction` | Min/max weight bounds on any inner model |
| `RegimeWeightConstrainedConstruction` | Regime-dependent weight constraints |
| `TurnoverPenalizedConstruction` | L1 turnover penalty decorator (stateful) |

### Covariance Estimators (4)
`SampleCovarianceEstimator`, `ExponentiallyWeightedCovarianceEstimator`, `LedoitWolfShrinkageEstimator`, `DenoisedCovarianceEstimator`

### Downside Risk Measures (3)
`CVaRRiskMeasure`, `DownsideDeviationRiskMeasure`, `CDaRRiskMeasure` — all guard against empty scenarios with `CalculationException`

### Analytics (7)
`BrinsonFachlerAttributor`, `FactorRegressor`, `CorrelationAnalyzer`, `DrawdownAnalyzer`, `EffectiveNumberOfBets`, `WalkForwardOptimizer`, `MonteCarloSimulator`

### Risk Management (5)
`RiskManager` (composite, first-rejection short-circuit) with `MaxDrawdownRule`, `MaxPositionSizeRule`, `MaxSectorExposureRule`, `DrawdownCircuitBreaker`

### Indicators (6)
`SimpleMovingAverage`, `ExponentialMovingAverage`, `RealizedVolatility`, `MomentumScore`, `SpreadIndicator`, `RateOfChangeIndicator`

### Regime Detection
`GrowthInflationRegimeClassifier` — Four-quadrant classification with configurable deadband hysteresis

### Universe Filtering (6)
`MinAumFilter`, `MinAgeFilter`, `LiquidityFilter`, `SupersessionFilter`, `CompositeUniverseSelector`, `CompositeTimedUniverseSelector`, `DynamicUniverse`

### Caching (6 Decorators)
L1 memory cache (`CachingMarketDataFetcher`, `CachingEconomicDataFetcher`, `CachingFactorDataFetcher`) with IEnumerable materialization, faulted-entry eviction, and caller-independent cancellation + L2 CSV write-through (`WriteThroughMarketDataFetcher`, `WriteThroughEconomicDataFetcher`, `WriteThroughFactorDataFetcher`) with immediate API failure propagation

### Reporting
`HtmlReportGenerator` (self-contained HTML tearsheet), `BenchmarkComparisonReport` (side-by-side portfolio vs benchmark)

### DI Registration
`ServiceCollectionExtensions.AddBoutquinTrading()` with `BacktestOptions`, `CostModelOptions`, `RiskManagementOptions`, `CacheOptions`, `CalendarOptions`

## Disclaimer

Boutquin.Trading is open-source software provided under the Apache 2.0 License. It is a general-purpose research and backtesting tool intended for educational purposes only.

**This software does not constitute financial advice.** All historical performance data represents backtested results computed using actual historical index and ETF return data. Backtested performance is hypothetical and does not represent actual trading. Actual investment results may differ materially. Past performance is not indicative of future results.

The software authors are not registered investment advisers, portfolio managers, or financial planners. Use of this software to make investment decisions is entirely at your own risk. Before making any investment decision, consult with a qualified financial professional who understands your individual circumstances, goals, and risk tolerance.

## Contributing

Please read the [contributing guidelines](https://github.com/boutquin/Boutquin.Trading/blob/main/CONTRIBUTING.md) first.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.

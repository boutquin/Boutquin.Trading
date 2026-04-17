# Example 02 — Multi-Asset with Risk Parity

A three-asset portfolio using monthly risk-parity rebalancing. Demonstrates `ConstructionModelStrategy`, `RiskParityConstruction`, and `LedoitWolfConstantCorrelationEstimator`.

## What This Shows

- Wiring a portfolio construction model through DI
- Monthly calendar rebalancing trigger
- Covariance estimator selection
- Using `DynamicWeightPositionSizer` to size positions from computed weights
- Reading per-model weight history from `ConstructionModelStrategy.LastComputedWeights`

## Code

```csharp
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.Trading.Application.Configuration;
using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Rebalancing;
using Boutquin.Trading.Application.Strategies;
using Microsoft.Extensions.DependencyInjection;

// --- Dependency injection ---
var services = new ServiceCollection();
services.AddBoutquinTrading(configuration);   // wires backtest options, cost model, risk manager
var sp = services.BuildServiceProvider();

// --- Assets ---
var baseCurrency = new CurrencyCode("USD");
var assets = new[]
{
    new Symbol("SPY"),   // US equities
    new Symbol("TLT"),   // US long-duration bonds
    new Symbol("GLD"),   // Gold
};
var assetCurrencies = assets.ToDictionary(a => a, _ => baseCurrency);

// --- Construction model ---
var covarianceEstimator = new LedoitWolfConstantCorrelationEstimator();
var constructionModel   = new RiskParityConstruction(covarianceEstimator);
var rebalancingTrigger  = new CalendarRebalancingTrigger();

var strategy = new ConstructionModelStrategy(
    name: "RiskParity",
    assetCurrencies: assetCurrencies,
    initialCash: new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, 100_000m } },
    startDate: new DateOnly(2020, 1, 2),
    constructionModel: constructionModel,
    rebalancingTrigger: rebalancingTrigger,
    rebalancingFrequency: RebalancingFrequency.Monthly,
    positionSizer: new DynamicWeightPositionSizer(baseCurrency),
    orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
    lookbackDays: 252);

// Wire strategy into Portfolio and run as in Example 01.
// Retrieve computed weights after each rebalance:
// var weights = strategy.LastComputedWeights;
// foreach (var (asset, weight) in weights)
//     Console.WriteLine($"{asset}: {weight:P1}");
```

## Notes on Risk Parity

- **Covariance estimator** — `LedoitWolfConstantCorrelationEstimator` is the recommended default for equity portfolios. It shrinks the sample covariance toward a target with equal pairwise correlations, reducing estimation error on short history windows.
- **Lookback** — 252 trading days (one year) is a common default. Reduce to 126 (six months) for more regime-responsive weights; increase to 504 (two years) for more stable but slower-reacting weights.
- **Rebalancing** — Monthly calendar rebalancing is a reasonable balance between transaction cost and tracking. Use `ThresholdRebalancingTrigger` to rebalance only when drift exceeds a band.
- **Negative MRC guard** — `RiskParityConstruction` throws `CalculationException` when any marginal risk contribution is non-positive. This occurs with hedging assets in certain correlation regimes. Consider switching to `HierarchicalRiskParityConstruction` (never inverts the covariance matrix) when the universe may include negatively correlated assets.

## Next Steps

Add transaction costs (Example 03) or compare covariance estimators (Example 04).

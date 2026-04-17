# Example 02 — Multi-Asset with Minimum Variance

A three-asset portfolio using monthly minimum-variance rebalancing. Demonstrates `ConstructionModelStrategy`, `MinimumVarianceConstruction`, and `LedoitWolfConstantCorrelationEstimator`.

## What This Shows

- Wiring a portfolio construction model through `ConstructionModelStrategy`
- Monthly calendar rebalancing trigger
- Covariance estimator selection
- Using `DynamicWeightPositionSizer` to size positions from computed weights
- Reading per-model weight history from `ConstructionModelStrategy.LastComputedWeights`

## Code

```csharp
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.Trading.Application.CovarianceEstimators;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Rebalancing;
using Boutquin.Trading.Application.Strategies;

// --- Assets ---
const CurrencyCode baseCurrency = CurrencyCode.USD;
var spy = new Symbol("SPY");
var tlt = new Symbol("TLT");
var gld = new Symbol("GLD");
Symbol[] assets = [spy, tlt, gld];
var assetCurrencies = new Dictionary<Symbol, CurrencyCode>
{
    { spy, CurrencyCode.USD },
    { tlt, CurrencyCode.USD },
    { gld, CurrencyCode.USD },
};

// --- Construction model ---
var covarianceEstimator = new LedoitWolfConstantCorrelationEstimator();
var constructionModel   = new MinimumVarianceConstruction(covarianceEstimator);
var rebalancingTrigger  = new CalendarRebalancingTrigger();

var strategy = new ConstructionModelStrategy(
    name: "MinVar-3Asset",
    assetCurrencies: assetCurrencies,
    initialCash: new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, 100_000m } },
    startDate: new DateOnly(2022, 1, 3),
    constructionModel: constructionModel,
    rebalancingTrigger: rebalancingTrigger,
    rebalancingFrequency: RebalancingFrequency.Monthly,
    positionSizer: new DynamicWeightPositionSizer(baseCurrency),
    orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
    lookbackDays: 60);

// Wire strategy into Portfolio and run as in Example 01.
// Retrieve computed weights after each rebalance:
// var weights = strategy.LastComputedWeights;
// foreach (var (asset, weight) in weights)
//     Console.WriteLine($"{asset}: {weight:P1}");
```

## Notes on Minimum Variance

- **Covariance estimator** — `LedoitWolfConstantCorrelationEstimator` is the recommended default for equity portfolios. It shrinks the sample covariance toward a target with equal pairwise correlations, reducing estimation error on short history windows.
- **Lookback** — 60 trading days is adequate for a 3-asset universe with stable correlations. Increase to 252 for slower-reacting but smoother weights.
- **Rebalancing** — Monthly calendar rebalancing is a reasonable balance between transaction cost and tracking. Use `ThresholdRebalancingTrigger` to rebalance only when drift exceeds a band.
- **Alternative models** — `RiskParityConstruction` equalises marginal risk contributions across assets; prefer it when you want to balance risk allocation rather than simply minimise total variance. Note that `RiskParityConstruction` requires strictly positive marginal risk contributions, which may not hold for strongly negatively correlated assets.

## Next Steps

Add transaction costs (Example 03) or compare covariance estimators (Example 04).

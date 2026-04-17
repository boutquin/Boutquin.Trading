# Example 03 — Cost and Slippage Models

Add realistic transaction costs to a backtest. Demonstrates the `PercentageOfValueCostModel`, `PercentageSlippageModel`, and DI-based cost model registration.

## What This Shows

- Choosing between fixed, per-share, percentage, and tiered cost models
- Choosing between fixed, percentage, spread, and volume-share slippage models
- Registering cost and slippage models through `CostModelOptions`
- Measuring commission drag between two backtest runs

## Via Dependency Injection

```json
// appsettings.json
{
  "CostModel": {
    "CommissionType": "PercentageOfValue",
    "CommissionRate": 0.001,
    "SlippageType": "PercentageSlippage",
    "SlippageAmount": 0.0005
  }
}
```

The above configuration charges 0.10% commission and 0.05% slippage per trade (round-trip cost ≈ 0.30%).

## Direct Construction

```csharp
using Boutquin.Trading.Application.CostModels;
using Boutquin.Trading.Application.SlippageModels;

// 1 bp commission + 0.5 bp slippage
var costModel     = new PercentageOfValueCostModel(commissionRate: 0.0001m);
var slippageModel = new PercentageSlippageModel(slippageFraction: 0.00005m);

var broker = new SimulatedBrokerage(costModel, slippageModel);
```

## Cost Model Reference

| Model | Class | When to Use |
|-------|-------|-------------|
| No cost | `NoCostModel` | Frictionless baseline |
| Fixed per trade | `FixedCostModel` | Flat-fee brokers |
| Per share | `PerShareCostModel` | Equity volume-based pricing |
| Percentage of value | `PercentageOfValueCostModel` | Most realistic for ETF/equity backtesting |
| Tiered | `TieredCostModel` | Volume-discount schedules |
| Composite | `CompositeCostModel` | Combines base commission + regulatory fees |

## Slippage Model Reference

| Model | Class | When to Use |
|-------|-------|-------------|
| No slippage | `NoSlippageModel` | Zero slippage baseline |
| Fixed | `FixedSlippageModel` | Fixed tick offset |
| Percentage | `PercentageSlippageModel` | Proportional market impact |
| Spread | `SpreadSlippageModel` | Half-spread cost on each fill |
| Volume share | `VolumeShareSlippageModel` | Impact proportional to volume fraction consumed |

## Measuring Commission Drag

Run the same strategy twice — once with `NoCostModel` / `NoSlippageModel` and once with your production cost model — and compare the annualized return delta:

```csharp
var tearSheetNoCost  = TearSheet.Create(portfolioNoCost.GetEquityCurve());
var tearSheetWithCost = TearSheet.Create(portfolioWithCost.GetEquityCurve());

decimal drag = tearSheetNoCost.AnnualizedReturn - tearSheetWithCost.AnnualizedReturn;
Console.WriteLine($"Annual commission drag: {drag:P2}");
```

Typical drag for monthly-rebalancing ETF portfolios is 0.05%–0.30% per year depending on turnover and spread.

# Example 01 — Buy and Hold

The simplest possible backtest: a single-asset buy-and-hold strategy with no commissions, no slippage, and no rebalancing. Use this to verify the event pipeline is wired correctly before adding complexity.

## What This Shows

- Constructing a `Portfolio` with a single strategy
- Registering the four event handlers (`MarketEventHandler`, `SignalEventHandler`, `OrderEventHandler`, `FillEventHandler`)
- Running `BackTest.RunAsync` and reading the equity curve
- Extracting a `TearSheet` with summary performance metrics

## Code

```csharp
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.Trading.Application;
using Boutquin.Trading.Application.Brokers;
using Boutquin.Trading.Application.EventHandlers;
using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.Helpers;
using Boutquin.Trading.Domain.Interfaces;

// --- Configuration ---
var baseCurrency = new CurrencyCode("USD");
var spy = new Symbol("SPY");

var assetCurrencies = new Dictionary<Symbol, CurrencyCode> { { spy, baseCurrency } };
var initialCash = new SortedDictionary<CurrencyCode, decimal> { { baseCurrency, 100_000m } };
var fixedWeights = new Dictionary<Symbol, decimal> { { spy, 1m } };

// --- Strategy ---
var strategy = new BuyAndHoldStrategy(
    name: "BuyAndHold",
    assetCurrencies: assetCurrencies,
    initialCash: initialCash,
    startDate: new DateOnly(2020, 1, 2),
    orderPriceCalculationStrategy: new ClosePriceOrderPriceCalculationStrategy(),
    positionSizer: new FixedWeightPositionSizer(fixedWeights, baseCurrency));

// --- Event handlers ---
var handlers = new Dictionary<Type, IEventHandler>
{
    { typeof(MarketEvent),  new MarketEventHandler() },
    { typeof(SignalEvent),  new SignalEventHandler() },
    { typeof(OrderEvent),   new OrderEventHandler() },
    { typeof(FillEvent),    new FillEventHandler() },
};

// --- Portfolio and brokerage ---
var broker = new SimulatedBrokerage();
var portfolio = new Portfolio(
    baseCurrency,
    new ReadOnlyDictionary<string, IStrategy>(
        new Dictionary<string, IStrategy> { { strategy.Name, strategy } }),
    assetCurrencies,
    handlers,
    broker);

// --- Run backtest ---
// Supply market data via IBacktestDataset (see docs/examples/README.md).
// For a fully wired example see src/Sample/Program.cs.
var backtest = new BackTest(
    portfolio,
    dataset,          // IBacktestDataset — omitted here; see Example 02 for setup
    startDate: new DateOnly(2020, 1, 2),
    endDate:   new DateOnly(2023, 12, 29));

await backtest.RunAsync();

// --- Results ---
var equityCurve = portfolio.GetEquityCurve();
var tearSheet   = TearSheet.Create(equityCurve);

Console.WriteLine($"CAGR:         {tearSheet.AnnualizedReturn:P2}");
Console.WriteLine($"Max Drawdown: {tearSheet.MaxDrawdown:P2}");
Console.WriteLine($"Sharpe:       {tearSheet.SharpeRatio:F2}");
```

## Expected Output (illustrative)

```
CAGR:         11.42%
Max Drawdown: -23.87%
Sharpe:       0.71
```

Exact figures depend on the market data source and date range.

## Key Points

- `BuyAndHoldStrategy` generates a signal on the first bar and holds forever. No rebalancing.
- Orders fill at the next bar's Open price (next-bar Open fill — no look-ahead bias).
- With `FixedWeightPositionSizer` at weight 1.0 and no cash buffer, the entire cash balance is allocated to SPY on day one.
- `TearSheet.Create` requires at least 252 observations for annualized metrics to be meaningful.

## Next Steps

Add commissions (Example 03) or switch to a multi-asset construction model (Example 02).

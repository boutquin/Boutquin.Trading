# Example 05 — Analytics and Attribution

Post-backtest analytics: Brinson-Fachler attribution, factor regression, correlation analysis, and drawdown identification.

## What This Shows

- Running Brinson-Fachler single-period attribution against a benchmark
- Running multi-factor OLS regression against Fama-French factors
- Computing portfolio correlation matrix and Effective Number of Bets
- Identifying discrete drawdown periods

## Brinson-Fachler Attribution

Decomposes active return into allocation, selection, and interaction effects.

```csharp
using Boutquin.Trading.Application.Analytics;
using Boutquin.Trading.Domain.Analytics;

// Sector weights and returns for portfolio and benchmark
Symbol[] assets = [new Symbol("SPY"), new Symbol("TLT")];
var portfolioWeights   = new Dictionary<Symbol, decimal> { { assets[0], 0.6m }, { assets[1], 0.4m } };
var benchmarkWeights   = new Dictionary<Symbol, decimal> { { assets[0], 0.5m }, { assets[1], 0.5m } };
var portfolioReturns   = new Dictionary<Symbol, decimal> { { assets[0], 0.08m }, { assets[1], 0.03m } };
var benchmarkReturns   = new Dictionary<Symbol, decimal> { { assets[0], 0.07m }, { assets[1], 0.04m } };

BrinsonFachlerResult result = BrinsonFachlerAttributor.Attribute(
    assets, portfolioWeights, benchmarkWeights, portfolioReturns, benchmarkReturns);

Console.WriteLine($"Allocation effect:   {result.AllocationEffect:P2}");
Console.WriteLine($"Selection effect:    {result.SelectionEffect:P2}");
Console.WriteLine($"Interaction effect:  {result.InteractionEffect:P2}");
Console.WriteLine($"Total active return: {result.TotalActiveReturn:P2}");
```

## Factor Regression

Multi-factor OLS regression of portfolio returns against Fama-French factors.

```csharp
using Boutquin.Trading.Application.Analytics;
using Boutquin.Trading.Domain.Analytics;

// portfolioReturns: daily portfolio excess returns (T observations)
// factorReturns: K arrays of T factor returns each
FactorRegressionResult result = FactorRegressor.Regress(
    portfolioReturns: portfolioExcessReturns,
    factorNames: ["Mkt-RF", "SMB", "HML"],
    factorReturns: [marketExcessReturns, smbReturns, hmlReturns]);

Console.WriteLine($"Alpha (annualised): {result.Alpha * 252:P2}");
Console.WriteLine($"Market beta:        {result.FactorLoadings["Mkt-RF"]:F3}");
Console.WriteLine($"R-squared:          {result.RSquared:P1}");
```

> `FactorRegressor` uses Householder QR decomposition via `Boutquin.Numerics.Solvers.OrdinaryLeastSquares<decimal>` for 28-digit accuracy. Collinear inputs throw `CalculationException`.

## Correlation Analysis

```csharp
using Boutquin.Trading.Application.Analytics;

// assets: Symbol[] (N assets)
// returns: decimal[][] — N arrays of T returns each
// weights: decimal[] — portfolio weights, length N, sum to 1
CorrelationAnalysisResult result = CorrelationAnalyzer.Analyze(assets, returns, weights);

Console.WriteLine($"Diversification ratio: {result.DiversificationRatio:F3}");
Console.WriteLine($"Corr(SPY, TLT):        {result.CorrelationMatrix[0, 1]:F3}");

// Effective Number of Bets (Meucci 2009)
decimal enb = EffectiveNumberOfBets.ComputeFromReturns(returns);
Console.WriteLine($"ENB: {enb:F1} of {assets.Length} assets");
```

## Drawdown Analysis

```csharp
using Boutquin.Trading.Application.Analytics;
using Boutquin.Trading.Domain.Analytics;

var equityCurve = portfolio.GetEquityCurve();
IReadOnlyList<DrawdownPeriod> drawdowns = DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

foreach (var dd in drawdowns.OrderByDescending(d => d.Depth).Take(3))
{
    Console.WriteLine(
        $"Peak {dd.StartDate}: {dd.Depth:P1}, " +
        $"duration {dd.DurationDays} trading days, " +
        $"recovery {(dd.RecoveryDate.HasValue ? dd.RecoveryDate.Value.ToString() : "ongoing")}");
}
```

## HTML Tearsheet

Generate a self-contained HTML report with embedded SVG charts:

```csharp
using Boutquin.Trading.Application.Reporting;

string html = HtmlReportGenerator.Generate(
    equityCurve: portfolio.GetEquityCurve(),
    benchmarkEquityCurve: benchmark.GetEquityCurve(),
    title: "Risk Parity vs SPY");

await File.WriteAllTextAsync("tearsheet.html", html);
```

The generated file includes: equity curve (normalized to 100), drawdown area chart, metrics table, and monthly returns heatmap. No external JavaScript dependencies.

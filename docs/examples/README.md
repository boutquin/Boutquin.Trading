# Examples

This directory contains worked examples for the most common Boutquin.Trading workflows. Each example is self-contained and progressively more realistic.

| Example | File | Focus |
|---------|------|-------|
| 01 — Buy and hold | [01-buy-and-hold.md](01-buy-and-hold.md) | Minimal backtest: single asset, fixed weight, no commission |
| 02 — Multi-asset with risk parity | [02-risk-parity.md](02-risk-parity.md) | Portfolio construction with monthly rebalancing |
| 03 — Cost and slippage models | [03-cost-and-slippage.md](03-cost-and-slippage.md) | Adding realistic transaction costs |
| 04 — Covariance estimator selection | [04-covariance-estimators.md](04-covariance-estimators.md) | Ledoit-Wolf, Denoised, and QIS estimators compared |
| 05 — Analytics and attribution | [05-analytics-attribution.md](05-analytics-attribution.md) | Brinson-Fachler attribution and factor regression |

## Running the Sample Project

The `Boutquin.Trading.Sample` project in `src/Sample/` demonstrates minimal configuration. To run it:

```bash
dotnet run --project src/Sample/Boutquin.Trading.Sample.csproj
```

For a full backtest from configuration, use the `BackTest` project:

```bash
dotnet run --project src/BackTest/Boutquin.Trading.BackTest.csproj
```

## Common Setup

All examples assume the following NuGet packages are referenced:

```xml
<PackageReference Include="Boutquin.Trading.Domain" />
<PackageReference Include="Boutquin.Trading.Application" />
```

For dependency injection:

```csharp
using Boutquin.Trading.Application.Configuration;

services.AddBoutquinTrading(configuration);
```

# Boutquin.Trading.Application

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.application?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

## Application Layer

The core application layer for a multi-asset, multi-currency, multi-strategy, event-driven trading platform. Provides backtesting, portfolio construction, analytics, risk management, and DI registration.

### Key Components

| Component | Description |
|-----------|-------------|
| `Portfolio` | Multi-currency cash management, position tracking, equity curve computation |
| `BackTest` | Event-driven backtesting engine with market data prefetch |
| `SimulatedBrokerage` | Market, limit, stop, and stop-limit order execution with slippage and commission models |

### Strategies
- `BuyAndHoldStrategy` — Static allocation, no rebalancing
- `RebalancingBuyAndHoldStrategy` — Periodic rebalancing to target weights
- `ConstructionModelStrategy` — Dynamic weights from portfolio construction models

### Portfolio Construction (8 Models)
`EqualWeight`, `InverseVolatility`, `MinimumVariance`, `MeanVariance`, `RiskParity`, `BlackLitterman`, `TacticalOverlay`, `VolatilityTargeting`

### Analytics
`BrinsonFachlerAttributor`, `FactorRegressor`, `CorrelationAnalyzer`, `DrawdownAnalyzer`, `WalkForwardOptimizer`, `MonteCarloSimulator`

### Risk Management
`RiskManager` with `MaxDrawdownRule`, `MaxPositionSizeRule`, `MaxSectorExposureRule`

### Caching
Transparent L1 memory cache + L2 CSV write-through decorators for market data, economic data, and factor data fetchers.

## Contributing

Please read the [contributing guidelines](https://github.com/boutquin/Boutquin.Trading/blob/main/CONTRIBUTING.md) first.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/main/LICENSE.txt) for details.

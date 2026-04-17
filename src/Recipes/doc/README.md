# Boutquin.Trading.Recipes

Recipe layer bridging the Boutquin.Trading domain to the Boutquin.MarketData kernel.

Provides `BacktestDatasetBuilder` for materializing market data via `IDataPipeline` into an `IBacktestDataset` consumed by the backtest engine.

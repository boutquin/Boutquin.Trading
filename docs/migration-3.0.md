# Migrating Trading to MarketData 3

Current source targets .NET 10 and .NET 11 and consumes the complete published MarketData and Adapter 3.0.0 family. Numerics remains at published 2.0.0. Regenerate the solution lockfiles and verify signed dependency archives together with these package pins.

## Declare the basis before fetching

Set `BacktestDatasetSpec.SymbolPriceBases` for every requested ticker. Use the selected adapter's `DeliveredPriceBasis`, such as `TiingoPriceBarAdapter.DeliveredPriceBasis` or `TwelveDataPriceBarAdapter.DeliveredPriceBasis`. Trading requires adjusted-close prices containing splits and dividends. Missing symbols and non-total-return bases are rejected before any provider request.

The builder copies the selected mappings before asynchronous reads. Ordinary exhausted provider requests remain fetch issues; `PriceBasisMismatchException` and `PriceBasisNotSupportedException` abort construction and must be handled as basis-integrity failures.

## Preserve provenance and accounting

The kernel validates declared delivery bases. A custom non-kernel pipeline that returns bars without proven price-basis provenance retains its data with `PRICE_BASIS_UNPROVEN`. Do not interpret that warning as proof of the requested basis. Proven non-total-return deliveries fail explicitly.

Total-return adjusted prices already include splits and dividends. Dividend and corporate-action collections are informational for these datasets; applying them again would double-count adjustments. Price basis is also part of the MarketData 3 cache/series identity. Follow the [MarketData migration guide](https://github.com/boutquin/Boutquin.MarketData/blob/v3.0.0/docs/migration-3.0.md) when moving older cached data; do not relabel adjusted data as raw.

## Verification

Both target suites retain the existing accounting and numerical checks. .NET 10 keeps its financial goldens, while .NET 11 uses native decimal conversions checked against independent cash/holdings references. New basis tests cover missing mappings, unsupported bases, provenance failures and ordinary provider outages.

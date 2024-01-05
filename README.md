# Boutquin.Trading

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.domain?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

*** Very much a work in progress ***

A multi-asset, multi-strategy, event-driven trading platform for back testing strategies with portfolio-based risk management and %-per-strategy capital allocation.

## Overview

### Domain

This will contain all entities, enums, exceptions, interfaces, types and logic specific to the domain layer.

Here are few key details:

IPortfolio Interface: The interface defines the properties and methods that a portfolio should have. It includes properties like IsLive, EventProcessor, Broker, Strategies, AssetCurrencies, HistoricalMarketData, HistoricalFxConversionRates, and EquityCurve which give essential data about the portfolio.

The implementation of the IPortfolio Interface is in the application layer.

At the lower level, a key extension class here is the [DecimalArrayExtensions class](./doc/DecimalArrayExtensions.md), a static class that provides extension methods for working with arrays of decimal values. It includes methods for calculating the Sharpe Ratio and Annualized Sharpe Ratio of daily returns for a given array of decimal values.

## Contributing

If you'd like to contribute to the development of Boutquin.Trading, please feel free to submit a pull request or open an issue with your suggestions or improvements.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/master/LICENSE.txt) for more information.

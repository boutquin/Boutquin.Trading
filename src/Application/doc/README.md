# Boutquin.Trading.Application

![Nuget](https://img.shields.io/nuget/vpre/boutquin.trading.application?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/boutquin.trading?style=for-the-badge)

*** Very much a work in progress ***

# Application Layer

This project contains the application layer for a multi-asset, multi-currency, multi-strategy, event-driven trading platform for back testing strategies with portfolio-based risk management and %-per-strategy capital allocation.. 

Here are few key details:

IPortfolio Interface: The interface defines the properties and methods that a portfolio should have. It includes properties like IsLive, EventProcessor, Broker, Strategies, AssetCurrencies, HistoricalMarketData, HistoricalFxConversionRates, and EquityCurve which give essential data about the portfolio.

Portfolio Class: This class is an implementation of the IPortfolio interface. It provides the concrete implementation of the properties and methods defined in the interface. Note that few properties throw a NotImplementedException which signifies that those properties' implementation is not yet done in this class.

The methods are mainly focused on portfolio management in an automated trading system:

1. HandleEventAsync: Asynchronously handle an event using the EventProcessor.
2. UpdateHistoricalData: Update the historical market data and foreign exchange conversion rates based on the new market event.
3. UpdateCashForDividend: Update the cash of each strategy holding a particular asset when a dividend event occurs.
4. SubmitOrderAsync: Submit an order to the brokerage.
5. GenerateSignals: Generate trading signals based on the market data.
6. UpdatePosition and UpdateCash: Update the asset quantity in a strategy's position and the cash balance respectively.
7. UpdateEquityCurve: Update the equity curve based on the total portfolio value.
8. AdjustPositionForSplit and AdjustHistoricalDataForSplit: Adjust the positions and the historical market data respectively when a stock split occurs.
9. etStrategy and GetAssetCurrency: Get the strategy or asset currency based on the name.
10. CalculateTotalPortfolioValue: Calculate the total value of the portfolio.

## Contributing

If you'd like to contribute to the development of the Security Master Data Access Layer, please feel free to submit a pull request or open an issue with your suggestions or improvements.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](https://github.com/boutquin/Boutquin.Trading/blob/master/LICENSE.txt) for more information.

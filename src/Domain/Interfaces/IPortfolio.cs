// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Domain.Interfaces;
/// <summary>
/// The IPortfolio interface defines the structure and behavior of a portfolio in a trading system.
/// It provides methods and properties to manage the portfolio's strategies, assets, historical data, 
/// and to perform various portfolio management operations such as allocating capital, 
/// updating cash balances, submitting orders, generating trading signals, updating positions, 
/// updating historical data, updating the equity curve, and adjusting data and positions for stock splits.
/// It includes properties for an event processor, a brokerage, strategies, asset currencies, 
/// historical market data, historical foreign exchange conversion rates, and the equity curve.
/// </summary>
/// <remarks>
/// Implementations of this interface should ensure that the integrity and consistency of the portfolio state 
/// is maintained throughout all operations. They should also ensure that the necessary checks and validations 
/// are in place to prevent the execution of invalid operations that could lead to an inconsistent portfolio state.
/// </remarks>
public interface IPortfolio
{
    /// <summary>
    /// Indicates whether the portfolio is being run in live mode.
    /// </summary>
    /// <value>True if the portfolio is being run in live mode, false otherwise.</value>
    /// <remarks>
    /// This property is used to differentiate between a live trading environment and a backtest or simulation environment.
    /// In live mode, the portfolio operates in real-time and is subject to live market data. In a backtest or simulation mode, the portfolio operates on historical data.
    /// </remarks>
    public bool IsLive { get; }

    /// <summary>
    /// When true, dividends credited via <see cref="UpdateCashForDividend(Symbol, decimal, decimal)"/> are
    /// automatically reinvested into the paying asset (whole shares at Close price).
    /// </summary>
    bool EnableDividendReinvestment { get; }

    /// <summary>
    /// The base currency of the portfolio.
    /// </summary>
    /// <value>The base currency of the portfolio.</value>
    /// <remarks>
    /// The base currency is the currency in which the portfolio's value is calculated and reported.
    /// All cash balances, asset prices, and portfolio values are converted to the base currency for calculations and reporting.
    /// </remarks>
    Currency BaseCurrency { get; }

    /// <summary>
    /// The EventProcessor property represents a system to process events for the portfolio.
    /// </summary>
    public IEventProcessor EventProcessor { get; }

    /// <summary>
    /// The Strategies property represents a read-only dictionary of strategies used in the portfolio.
    /// </summary>
    IReadOnlyDictionary<string, IStrategy> Strategies { get; }

    /// <summary>
    /// The AssetCurrencies property represents a read-only dictionary of assets and their respective currencies used in the portfolio.
    /// </summary>
    IReadOnlyDictionary<Symbol, Currency> AssetCurrencies { get; }

    /// <summary>
    /// The HistoricalMarketData property represents a sorted dictionary of historical market data used by the portfolio.
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>> HistoricalMarketData { get; }

    /// <summary>
    /// The FX rate tables the portfolio has received, by date. Each table is keyed by non-base
    /// currency and holds the number of base-currency units per 1 unit of the keyed currency
    /// (the contract documented on <see cref="Helpers.FxConversion"/>).
    /// </summary>
    SortedDictionary<DateOnly, SortedDictionary<Currency, decimal>> HistoricalFxConversionRates { get; }

    /// <summary>
    /// The EquityCurve property represents a sorted dictionary of equity values over time.
    /// </summary>
    SortedDictionary<DateOnly, decimal> EquityCurve { get; }

    /// <summary>
    /// A currency-tagged <see cref="Money"/> projection of <see cref="EquityCurve"/>, where
    /// each value is the equity amount tagged with the portfolio's <see cref="BaseCurrency"/>.
    /// </summary>
    /// <value>
    /// A read-only dictionary keyed by <see cref="DateOnly"/>; each value is a <see cref="Money"/>
    /// whose <see cref="Money.Amount"/> equals the corresponding <see cref="EquityCurve"/> entry and
    /// whose <see cref="Money.Currency"/> is <see cref="BaseCurrency"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// This is a boundary projection derived from the backing <see cref="EquityCurve"/> collection —
    /// the stored collection stays <see cref="decimal"/> (hot/internal; written from inside the
    /// per-bar loop), and this view is materialized at the reporting boundary. The default
    /// implementation maps each entry without entering any per-bar path, so it cannot perturb
    /// backtest throughput. It never throws.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<DateOnly, Money> EquityCurveAsMoney
    {
        get
        {
            var result = new Dictionary<DateOnly, Money>(EquityCurve.Count);
            foreach (var (timestamp, value) in EquityCurve)
            {
                result[timestamp] = new Money(value, BaseCurrency);
            }

            return result;
        }
    }

    /// <summary>
    /// Asynchronously handles the specified event, processing it using the portfolio's event processor.
    /// </summary>
    /// <param name="event">The event to handle. This represents an occurrence in the system that may affect the state of the portfolio.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains no value.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided event is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if an error occurs while processing the event.</exception>
    /// <remarks>
    /// This method processes the given event using the portfolio's event processor. The event represents something that has happened in the system,
    /// such as a change in the market, a change in the portfolio's assets, or a change in the portfolio's strategy.
    /// The portfolio's event processor is responsible for updating the state of the portfolio based on the event.
    /// Note that the event is processed asynchronously, so the method may return before the event processing has completed.
    /// Any errors that occur during the event processing are thrown as exceptions.
    /// </remarks>
    Task HandleEventAsync(IFinancialEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates historical data of the portfolio.
    /// </summary>
    /// <param name="marketEvent">Market event containing the updated historical data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the marketEvent parameter is null.</exception>
    void UpdateHistoricalData(MarketEvent marketEvent);

    /// <summary>
    /// Updates the cash balance for each strategy that holds a given asset in response to a dividend event.
    /// When <see cref="EnableDividendReinvestment"/> is true and <paramref name="currentPrice"/> is positive,
    /// dividends are automatically reinvested into whole shares at the current price.
    /// </summary>
    /// <param name="asset">The asset symbol for which the dividend event occurred.</param>
    /// <param name="dividendPerShare">The amount of dividend received per share.</param>
    /// <param name="currentPrice">The current Close price of the asset, used for DRIP reinvestment. Pass 0 to disable reinvestment.</param>
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    void UpdateCashForDividend(
        Symbol asset,
        decimal dividendPerShare,
        decimal currentPrice);

    /// <summary>
    /// Updates the cash balance for each strategy holding <paramref name="asset"/> in response to a
    /// dividend event, using currency-tagged <see cref="Money"/> amounts. The additive
    /// <see cref="Money"/> companion to
    /// <see cref="UpdateCashForDividend(Symbol, decimal, decimal)"/>.
    /// </summary>
    /// <param name="asset">The asset symbol for which the dividend event occurred.</param>
    /// <param name="dividendPerShare">
    /// The per-share dividend as a currency-tagged <see cref="Money"/>. Its
    /// <see cref="Money.Currency"/> must equal the asset's native currency
    /// (<see cref="GetAssetCurrency"/>).
    /// </param>
    /// <param name="currentPrice">
    /// The current Close price of the asset as a currency-tagged <see cref="Money"/>, used for DRIP
    /// reinvestment; must be the same currency as <paramref name="dividendPerShare"/>.
    /// </param>
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="Boutquin.Domain.Exceptions.CurrencyMismatchException">
    /// Thrown when <paramref name="dividendPerShare"/> or <paramref name="currentPrice"/> does not
    /// match the asset's native currency, so a dividend is never credited to the wrong currency.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Validates both <see cref="Money"/> currencies against the asset's native currency, then routes
    /// to the <see cref="decimal"/> core
    /// <see cref="UpdateCashForDividend(Symbol, decimal, decimal)"/>. The per-strategy dividend
    /// crediting and DRIP arithmetic remain <see cref="decimal"/>; <see cref="Money"/> is the
    /// construction-boundary guard only.
    /// </para>
    /// </remarks>
    void UpdateCashForDividend(
        Symbol asset,
        Money dividendPerShare,
        Money currentPrice)
    {
        var assetCurrency = GetAssetCurrency(asset);
        if (dividendPerShare.Currency != assetCurrency)
        {
            throw new CurrencyMismatchException(assetCurrency, dividendPerShare.Currency);
        }

        if (currentPrice.Currency != assetCurrency)
        {
            throw new CurrencyMismatchException(assetCurrency, currentPrice.Currency);
        }

        UpdateCashForDividend(asset, dividendPerShare.Amount, currentPrice.Amount);
    }

    /// <summary>
    /// Submits an order based on an OrderEvent.
    /// </summary>
    /// <param name="orderEvent">The OrderEvent containing the order information.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the order submission was successful.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when the orderEvent parameter is null.</exception>
    /// <remarks>
    /// This method is called when an order needs to be submitted to the brokerage.
    /// The method implementation should ensure that the necessary checks and validations are performed on the order event data 
    /// before creating and submitting the order to the brokerage.
    /// </remarks>
    Task<bool> SubmitOrderAsync(OrderEvent orderEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a spot FX conversion between two of a strategy's cash buckets to the brokerage, as
    /// <see cref="SubmitOrderAsync"/> submits an asset order. The conversion fills on the next bar and
    /// returns through the portfolio's event pipeline as an <see cref="FxFillEvent"/>.
    /// </summary>
    /// <param name="fxOrderEvent">The conversion to submit.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A task whose result indicates whether the brokerage accepted the order.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="fxOrderEvent"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown by the default implementation, and by any brokerage that does not trade FX. A portfolio
    /// implementation that routes FX orders overrides this member; <c>Portfolio</c> does.
    /// </exception>
    Task<bool> SubmitFxOrderAsync(FxOrderEvent fxOrderEvent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not route FX orders. Override IPortfolio.SubmitFxOrderAsync, or use an FxFundingPolicy of None.");

    /// <summary>
    /// Generates trading signals for each strategy in the portfolio based on updated market data.
    /// </summary>
    /// <param name="marketEvent">The MarketEvent containing the updated market data.</param>
    /// <returns>An enumerable of SignalEvent containing the generated signals for each strategy.</returns>
    /// <remarks>
    /// This method is called when new market data is available and trading signals need to be generated for the portfolio's strategies.
    /// The method implementation should ensure that the signal generation process for each strategy is performed 
    /// according to the strategy's signal generation rules and that the generated signals are correctly formatted and returned.
    /// </remarks>
    IEnumerable<SignalEvent> GenerateSignals(
        MarketEvent marketEvent);

    /// <summary>
    /// Updates the position for a given asset in a specific strategy.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="asset">The asset symbol.</param>
    /// <param name="quantity">The quantity of the asset to be updated.</param>
    /// <exception cref="System.ArgumentException">Thrown when the strategyName or asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a position for a given asset in a specific strategy needs to be updated.
    /// The method implementation should ensure that the position is updated correctly 
    /// and that the new position does not lead to an inconsistent portfolio state.
    /// </remarks>
    void UpdatePosition(
        string strategyName,
        Symbol asset,
        int quantity);

    /// <summary>
    /// Updates the cash balance for a specific strategy and currency.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="currency">The currency of the cash balance to be updated.</param>
    /// <param name="amount">The amount to be updated.</param>
    /// <exception cref="System.ArgumentException">Thrown when the strategyName parameter is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the currency parameter is not a defined value of the Currency enumeration.</exception>
    /// <remarks>
    /// This method is called when the cash balance for a specific strategy and currency needs to be updated.
    /// The method implementation should ensure that the cash balance is updated correctly and that the new cash balance does not lead to an inconsistent portfolio state.
    /// </remarks>
    void UpdateCash(
        string strategyName,
        Currency currency,
        decimal amount);

    /// <summary>
    /// Updates the cash balance for a specific strategy by a currency-tagged <see cref="Money"/>
    /// amount, taking the currency from <see cref="Money.Currency"/>. The additive <see cref="Money"/>
    /// companion to <see cref="UpdateCash(string, Currency, decimal)"/>.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="amount">
    /// The signed amount to add to the named strategy's cash bucket identified by
    /// <see cref="Money.Currency"/>. Its currency must already be an existing cash bucket of that
    /// strategy, so the wrong bucket is never silently created or credited.
    /// </param>
    /// <exception cref="System.ArgumentException">Thrown when the <paramref name="strategyName"/> parameter is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="Boutquin.Domain.Exceptions.CurrencyMismatchException">
    /// Thrown when <paramref name="amount"/>'s currency is not one of the named strategy's existing
    /// cash buckets (a wrong-currency credit).
    /// </exception>
    /// <remarks>
    /// <para>
    /// Routes to the strategy's currency-validating <see cref="IStrategy.UpdateCash(Money)"/>, which
    /// rejects a wrong-currency credit and otherwise forwards to the <see cref="decimal"/> core.
    /// Construction-boundary guard only; the stored cash collection stays <see cref="decimal"/>.
    /// </para>
    /// </remarks>
    void UpdateCash(
        string strategyName,
        Money amount)
    {
        var strategy = GetStrategy(strategyName);
        strategy.UpdateCash(amount);
    }

    /// <summary>
    /// Updates the equity curve for the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for the equity curve update.</param>
    /// <remarks>
    /// This method is called when the equity curve for the portfolio needs to be updated.
    /// The method implementation should ensure that the equity curve is updated correctly and that the updated equity curve accurately reflects the current state of the portfolio.
    /// </remarks>
    void UpdateEquityCurve(
        DateOnly timestamp);

    /// <summary>
    /// Adjusts positions for a specific asset due to a stock split.
    /// </summary>
    /// <param name="asset">The asset that has been split.</param>
    /// <param name="splitRatio">The ratio of the split.</param>
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a stock split has occurred, and the portfolio's positions need to be adjusted.
    /// The method implementation should ensure that the positions are adjusted correctly and that the adjusted positions do not lead to an inconsistent portfolio state.
    /// </remarks>
    void AdjustPositionForSplit(
        Symbol asset,
        decimal splitRatio);

    /// <summary>
    /// Adjusts historical data for a specific asset due to a stock split.
    /// </summary>
    /// <param name="asset">The asset that has been split.</param>
    /// <param name="splitRatio">The ratio of the split.</param>
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a stock split has occurred, and the portfolio's historical data needs to be adjusted.
    /// The method implementation should ensure that the historical data is adjusted correctly.
    /// </remarks>
    void AdjustHistoricalDataForSplit(
        Symbol asset,
        decimal splitRatio);

    /// <summary>
    /// Processes pending orders from the previous bar against the current bar's market data.
    /// Delegates to the internal brokerage. Used by the backtest engine to implement next-bar fills.
    /// </summary>
    /// <param name="date">The current bar's date.</param>
    /// <param name="dayData">The current bar's market data for all assets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes pending asset and FX orders from the previous bar against the current bar, in the
    /// order asset sells → FX conversions → asset buys. The default implementation forwards to the
    /// asset-only overload and ignores <paramref name="fxRates"/>; <c>Portfolio</c> overrides it to
    /// hand the bar's FX table to the brokerage so pending conversions can fill.
    /// </summary>
    /// <param name="date">The current bar's date.</param>
    /// <param name="dayData">The current bar's market data for all assets.</param>
    /// <param name="fxRates">The bar's resolved FX table: base-currency units per 1 unit of each keyed currency (see <see cref="Helpers.FxConversion"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        SortedDictionary<Currency, decimal> fxRates,
        CancellationToken cancellationToken = default) => ProcessPendingOrdersAsync(date, dayData, cancellationToken);

    /// <summary>
    /// Retrieves a strategy based on its name.
    /// </summary>
    /// <param name="strategyName">The name of the strategy to retrieve.</param>
    /// <returns>The strategy associated with the provided name.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the strategyName parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when a strategy needs to be retrieved based on its name.
    /// The method implementation should ensure that the correct strategy is retrieved, or an appropriate error is thrown if the strategy cannot be found.
    /// </remarks>
    IStrategy GetStrategy(string strategyName);

    /// <summary>
    /// Retrieves the currency of a specific asset.
    /// </summary>
    /// <param name="asset">The asset for which the currency is to be retrieved.</param>
    /// <returns>The currency associated with the provided asset.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the asset parameter is null, empty, or consists only of white-space characters.</exception>
    /// <remarks>
    /// This method is called when the currency of a specific asset needs to be retrieved.
    /// The method implementation should ensure that the correct currency is returned, or an appropriate error is thrown if the currency cannot be found.
    /// </remarks>
    Currency GetAssetCurrency(Symbol asset);

    /// <summary>
    /// Calculates the total value of the portfolio.
    /// </summary>
    /// <param name="timestamp">The timestamp for which the total value is calculated.</param>
    /// <returns>The total value of the portfolio.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the baseCurrency parameter is not a defined value of the Currency enumeration.</exception>
    /// <remarks>
    /// This method is called when the total value of the portfolio needs to be calculated.
    /// The method implementation should ensure that the calculation is accurate and that it correctly reflects the current state of the portfolio.
    /// </remarks>
    decimal CalculateTotalPortfolioValue(
        DateOnly timestamp);

    /// <summary>
    /// Calculates the total value of the portfolio as a currency-tagged <see cref="Money"/> in the
    /// portfolio's <see cref="BaseCurrency"/>. The additive <see cref="Money"/> companion to
    /// <see cref="CalculateTotalPortfolioValue"/>.
    /// </summary>
    /// <param name="timestamp">The timestamp for which the total value is calculated.</param>
    /// <returns>
    /// A <see cref="Money"/> whose <see cref="Money.Amount"/> equals
    /// <see cref="CalculateTotalPortfolioValue"/> for the same timestamp and whose
    /// <see cref="Money.Currency"/> is <see cref="BaseCurrency"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a boundary projection: the default implementation tags the existing
    /// <see cref="decimal"/> total with <see cref="BaseCurrency"/>. The per-strategy aggregation
    /// inside <see cref="CalculateTotalPortfolioValue"/> remains <see cref="decimal"/>;
    /// <see cref="Money"/> appears only at the reporting boundary.
    /// </para>
    /// </remarks>
    Money CalculateTotalPortfolioValueAsMoney(
        DateOnly timestamp)
        => new(CalculateTotalPortfolioValue(timestamp), BaseCurrency);
}

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
/// The IBrokerage interface represents the brokerage component in the
/// trading platform, providing methods for managing orders and
/// updating positions, as well as fetching account and market data.
/// This interface can be implemented by different classes for both
/// simulation and live trading scenarios.
/// </summary>
public interface IBrokerage
{
    /// <summary>
    /// Submits an order for execution by the brokerage. The method should
    /// handle different order types (e.g. Market, Limit, Stop, StopLimit).
    /// </summary>
    /// <param name="order">The order to be executed, represented as an Order object.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <returns>A boolean indicating whether the order submission was successful.</returns>
    Task<bool> SubmitOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// An event that is raised when an order is filled, providing the
    /// FillEvent data to the subscribers.
    /// </summary>
    // A1 fix: Changed from EventHandler<FillEvent> to Func<object, FillEvent, Task>
    // so that async handlers can propagate exceptions instead of swallowing them via async void.
    event Func<object, FillEvent, Task> FillOccurred;

    /// <summary>
    /// Provides pre-buffered market data for backtest mode, eliminating per-order fetch calls.
    /// Default implementation is a no-op — only SimulatedBrokerage overrides this.
    /// </summary>
    [Obsolete("Buffered market data is not used. ProcessPendingOrdersAsync receives data directly.")]
    void SetBufferedMarketData(IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> data) { }

    /// <summary>
    /// Processes pending orders from the previous bar against the current bar's market data.
    /// Orders are filled at the current bar's Open price (market orders) or checked against
    /// the current bar's OHLC (limit/stop orders). This eliminates look-ahead bias by ensuring
    /// signals generated on bar T are filled at bar T+1 prices.
    /// Default implementation is a no-op — only SimulatedBrokerage overrides this.
    /// </summary>
    /// <param name="date">The current bar's date.</param>
    /// <param name="dayData">The current bar's market data for all assets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Submits a spot FX conversion between two of a strategy's cash buckets. It fills on the next
    /// bar, after that bar's asset sells and before its asset buys, at the bar's resolved FX rate.
    /// </summary>
    /// <param name="order">The conversion to queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the order was accepted.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown by the default implementation: a brokerage that does not override this member cannot
    /// trade FX, and a multi-currency strategy routed to it must fail loudly rather than run
    /// unfunded. <c>SimulatedBrokerage</c> overrides it.
    /// </exception>
    Task<bool> SubmitFxOrderAsync(FxOrder order, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not support FX orders. A strategy that holds assets in a currency other than " +
            "its funding currency needs a brokerage that implements IBrokerage.SubmitFxOrderAsync (SimulatedBrokerage does), " +
            "or an FxFundingPolicy of None.");

    /// <summary>
    /// Raised when an FX conversion fills, providing the <see cref="FxFillEvent"/> to subscribers.
    /// The default implementation never raises: a brokerage that does not trade FX has no FX fills.
    /// </summary>
    event Func<object, FxFillEvent, Task> FxFillOccurred
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Processes pending asset and FX orders from the previous bar against the current bar. Fills
    /// run in the order <b>asset sells → FX conversions → asset buys</b>, so sale proceeds are in
    /// hand when a conversion sells them on and converted cash is in hand when a buy draws on it.
    /// The default implementation forwards to the asset-only overload and ignores
    /// <paramref name="fxRates"/>.
    /// </summary>
    /// <param name="date">The current bar's date.</param>
    /// <param name="dayData">The current bar's market data for all assets.</param>
    /// <param name="baseCurrency">The portfolio's base currency; the one currency <paramref name="fxRates"/> never keys.</param>
    /// <param name="fxRates">The bar's resolved FX table: base-currency units per 1 unit of each keyed currency (see <see cref="Helpers.FxConversion"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        Currency baseCurrency,
        SortedDictionary<Currency, decimal> fxRates,
        CancellationToken cancellationToken = default) => ProcessPendingOrdersAsync(date, dayData, cancellationToken);
}

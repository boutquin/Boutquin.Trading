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
    void SetBufferedMarketData(IReadOnlyDictionary<DateOnly, SortedDictionary<ValueObjects.Asset, MarketData>> data) { }

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
        SortedDictionary<ValueObjects.Asset, MarketData> dayData,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

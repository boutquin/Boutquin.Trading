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

namespace Boutquin.Trading.Application.Brokers;

/// <summary>
/// SimulatedBrokerage is an implementation of the IBrokerage interface that simulates the behavior
/// of a real-world brokerage. It is intended to be used in backtesting trading strategies.
/// It uses an IMarketDataFetcher to retrieve market data which is used to simulate the execution of trades.
/// </summary>
public sealed class SimulatedBrokerage : IBrokerage
{
    private readonly IMarketDataFetcher _marketDataFetcher;

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerage class, using the provided IMarketDataFetcher.
    /// </summary>
    /// <param name="marketDataFetcher">An instance of an object implementing the IMarketDataFetcher interface.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the marketDataFetcher is null.
    /// </exception>
    public SimulatedBrokerage(IMarketDataFetcher marketDataFetcher)
    {
        Guard.AgainstNull(() => marketDataFetcher); // Throws ArgumentNullException

        _marketDataFetcher = marketDataFetcher;
    }

    /// <summary>
    /// Occurs when an order is filled. This can be subscribed to in order to receive fill events.
    /// </summary>
    // A1 fix: Changed from EventHandler<FillEvent> to Func<object, FillEvent, Task>
    // so that async handlers can propagate exceptions.
    public event Func<object, FillEvent, Task> FillOccurred;

    /// <summary>
    /// Submits an order for execution. The order is processed based on the available market data.
    /// In case of a Market order, it is immediately filled at the current market price.
    /// For Limit, Stop, and StopLimit orders, additional checks are performed.
    /// </summary>
    /// <param name="order">The order to be executed.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains a boolean value that is true if the
    /// order was successfully processed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the order is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the order type is not recognized.
    /// </exception>
    public async Task<bool> SubmitOrderAsync(Order order)
    {
        Guard.AgainstNull(() => order); // Throws ArgumentNullException

        // A5 fix: Filter market data to match the order's timestamp instead of using whatever date the fetcher returns
        var marketData = await _marketDataFetcher.FetchMarketDataAsync([order.Asset])
            .FirstOrDefaultAsync(kvp => kvp.Key == order.Timestamp).ConfigureAwait(false);

        if (marketData.Value == null || !marketData.Value.TryGetValue(order.Asset, out var assetMarketData))
        {
            return false;
        }

        // A1 fix: Handle*Order methods are now async to await FillOccurred
        var isOrderFilled = order.OrderType switch
        {
            OrderType.Market => await HandleMarketOrder(order, assetMarketData).ConfigureAwait(false),
            OrderType.Limit => await HandleLimitOrder(order, assetMarketData).ConfigureAwait(false),
            OrderType.Stop => await HandleStopOrder(order, assetMarketData).ConfigureAwait(false),
            OrderType.StopLimit => await HandleStopLimitOrder(order, assetMarketData).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(order.OrderType), order.OrderType, "OrderType is out of range"),
        };
        return isOrderFilled;
    }

    private async Task<bool> HandleMarketOrder(Order order, MarketData marketData)
    {
        var fillPrice = marketData.Close;
        var commission = CalculateCommission(order, fillPrice);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        // A1 fix: Await the async event handler so exceptions propagate
        if (FillOccurred != null)
        {
            await FillOccurred(this, fillEvent).ConfigureAwait(false);
        }
        return true;
    }

    private async Task<bool> HandleLimitOrder(Order order, MarketData marketData)
    {
        if (order.PrimaryPrice == null)
        {
            throw new InvalidOperationException("Limit price must be set for a limit order.");
        }

        // For simplicity, we fill limit orders if the limit price is better or equal to the close price
        var limitPrice = order.PrimaryPrice.Value;
        if ((order.TradeAction == TradeAction.Buy && limitPrice > marketData.Close) ||
            (order.TradeAction == TradeAction.Sell && limitPrice < marketData.Close))
        {
            return false;
        }

        var commission = CalculateCommission(order, limitPrice);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            limitPrice,
            order.Quantity,
            commission);

        if (FillOccurred != null)
        {
            await FillOccurred(this, fillEvent).ConfigureAwait(false);
        }
        return true;
    }

    private async Task<bool> HandleStopOrder(Order order, MarketData marketData)
    {
        if (order.PrimaryPrice == null)
        {
            throw new InvalidOperationException("Stop price must be set for a stop order.");
        }

        // For simplicity, we fill stop orders if the stop price is worse or equal to the close price
        var stopPrice = order.PrimaryPrice.Value;
        if ((order.TradeAction == TradeAction.Buy && stopPrice > marketData.Close) ||
            (order.TradeAction == TradeAction.Sell && stopPrice < marketData.Close))
        {
            return false;
        }

        var commission = CalculateCommission(order, stopPrice);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            stopPrice,
            order.Quantity,
            commission);

        if (FillOccurred != null)
        {
            await FillOccurred(this, fillEvent).ConfigureAwait(false);
        }
        return true;
    }

    private async Task<bool> HandleStopLimitOrder(Order order, MarketData marketData)
    {
        if (order.PrimaryPrice == null || order.SecondaryPrice == null)
        {
            throw new InvalidOperationException("Stop price and limit price must be set for a stop-limit order.");
        }

        var stopPrice = order.PrimaryPrice.Value;
        var limitPrice = order.SecondaryPrice.Value;

        // For simplicity, we fill stop-limit orders if the stop price is worse or equal to the close price
        // and the limit price is better or equal to the close price
        if ((order.TradeAction == TradeAction.Buy && (stopPrice > marketData.Close || limitPrice < marketData.Close)) ||
            (order.TradeAction == TradeAction.Sell && (stopPrice < marketData.Close || limitPrice > marketData.Close)))
        {
            return false;
        }

        var commission = CalculateCommission(order, limitPrice);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            limitPrice,
            order.Quantity,
            commission);

        if (FillOccurred != null)
        {
            await FillOccurred(this, fillEvent).ConfigureAwait(false);
        }
        return true;
    }

    private static decimal CalculateCommission(Order order, decimal fillPrice) =>
        // Simulate a commission of 0.1%
        fillPrice * order.Quantity * 0.001m;
}

// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
namespace Boutquin.Trading.Application.Brokers;

/// <summary>
/// SimulatedBrokerage is an implementation of the IBrokerage interface that simulates the behavior
/// of a real-world brokerage. It is intended to be used in backtesting trading strategies.
/// It uses an IMarketDataFetcher to retrieve market data which is used to simulate the execution of trades.
/// </summary>
public class SimulatedBrokerage : IBrokerage
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
    public event EventHandler<FillEvent> FillOccurred;

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

        var marketData = await _marketDataFetcher.FetchMarketDataAsync([order.Asset]).FirstOrDefaultAsync();

        if (marketData.Value == null || !marketData.Value.ContainsKey(order.Asset))
        {
            return false;
        }

        var assetMarketData = marketData.Value[order.Asset];
        var isOrderFilled = order.OrderType switch
        {
            OrderType.Market => HandleMarketOrder(order, assetMarketData),
            OrderType.Limit => HandleLimitOrder(order, assetMarketData),
            OrderType.Stop => HandleStopOrder(order, assetMarketData),
            OrderType.StopLimit => HandleStopLimitOrder(order, assetMarketData),
            _ => throw new ArgumentOutOfRangeException(nameof(order.OrderType), order.OrderType, "OrderType is out of range"),
        };
        return isOrderFilled;
    }

    private bool HandleMarketOrder(Order order, MarketData marketData)
    {
        var fillPrice = marketData.Close;
        var commission = CalculateCommission(order, fillPrice);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            fillPrice,
            order.Quantity,
            commission);

        FillOccurred?.Invoke(this, fillEvent);
        return true;
    }

    private bool HandleLimitOrder(Order order, MarketData marketData)
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
            limitPrice,
            order.Quantity,
            commission);

        FillOccurred?.Invoke(this, fillEvent);
        return true;
    }

    private bool HandleStopOrder(Order order, MarketData marketData)
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
            stopPrice,
            order.Quantity,
            commission);

        FillOccurred?.Invoke(this, fillEvent);
        return true;
    }

    private bool HandleStopLimitOrder(Order order, MarketData marketData)
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
            limitPrice,
            order.Quantity,
            commission);

        FillOccurred?.Invoke(this, fillEvent);
        return true;
    }
    
    private static decimal CalculateCommission(Order order, decimal fillPrice) =>
        // Simulate a commission of 0.1%
        fillPrice * order.Quantity * 0.001m;
}

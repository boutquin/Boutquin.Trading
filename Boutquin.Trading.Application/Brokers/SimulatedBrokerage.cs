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

using CostModels;
using SlippageModels;

/// <summary>
/// SimulatedBrokerage is an implementation of the IBrokerage interface that simulates the behavior
/// of a real-world brokerage. It is intended to be used in backtesting trading strategies.
/// It uses an IMarketDataFetcher to retrieve market data which is used to simulate the execution of trades.
/// </summary>
public sealed class SimulatedBrokerage : IBrokerage
{
    private readonly IMarketDataFetcher _marketDataFetcher;
    private readonly ITransactionCostModel _costModel;
    private readonly ISlippageModel _slippageModel;

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerage class with explicit cost and slippage models.
    /// </summary>
    /// <param name="marketDataFetcher">An instance of an object implementing the IMarketDataFetcher interface.</param>
    /// <param name="costModel">The transaction cost model for calculating commissions.</param>
    /// <param name="slippageModel">The slippage model for adjusting fill prices. Defaults to no slippage.</param>
    public SimulatedBrokerage(
        IMarketDataFetcher marketDataFetcher,
        ITransactionCostModel costModel,
        ISlippageModel? slippageModel = null)
    {
        Guard.AgainstNull(() => marketDataFetcher);
        Guard.AgainstNull(() => costModel);

        _marketDataFetcher = marketDataFetcher;
        _costModel = costModel;
        _slippageModel = slippageModel ?? new NoSlippage();
    }

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerage class with a flat commission rate.
    /// Backward-compatible constructor that wraps the rate in a <see cref="PercentageOfValueCostModel"/>.
    /// </summary>
    /// <param name="marketDataFetcher">An instance of an object implementing the IMarketDataFetcher interface.</param>
    /// <param name="commissionRate">The commission rate to apply to trades (default: 0.1%).</param>
    public SimulatedBrokerage(IMarketDataFetcher marketDataFetcher, decimal commissionRate = 0.001m)
        : this(marketDataFetcher, new PercentageOfValueCostModel(commissionRate))
    {
    }

    /// <summary>
    /// Occurs when an order is filled. This can be subscribed to in order to receive fill events.
    /// </summary>
    public event Func<object, FillEvent, Task>? FillOccurred;

    /// <summary>
    /// Submits an order for execution. The order is processed based on the available market data.
    /// </summary>
    /// <param name="order">The order to be executed.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains a boolean value that is true if the
    /// order was successfully processed; otherwise, false.</returns>
    public async Task<bool> SubmitOrderAsync(Order order)
    {
        Guard.AgainstNull(() => order);

        // A5 fix: Filter market data to match the order's timestamp
        var marketData = await _marketDataFetcher.FetchMarketDataAsync([order.Asset])
            .FirstOrDefaultAsync(kvp => kvp.Key == order.Timestamp).ConfigureAwait(false);

        if (marketData.Value == null || !marketData.Value.TryGetValue(order.Asset, out var assetMarketData))
        {
            return false;
        }

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
        var theoreticalPrice = marketData.Close;
        var fillPrice = _slippageModel.CalculateFillPrice(theoreticalPrice, order.Quantity, order.TradeAction);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

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

        var limitPrice = order.PrimaryPrice.Value;
        if ((order.TradeAction == TradeAction.Buy && marketData.Low > limitPrice) ||
            (order.TradeAction == TradeAction.Sell && marketData.High < limitPrice))
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(limitPrice, order.Quantity, order.TradeAction);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
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

        var stopPrice = order.PrimaryPrice.Value;
        if ((order.TradeAction == TradeAction.Buy && stopPrice > marketData.Close) ||
            (order.TradeAction == TradeAction.Sell && stopPrice < marketData.Close))
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(stopPrice, order.Quantity, order.TradeAction);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
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

        if ((order.TradeAction == TradeAction.Buy && (stopPrice > marketData.Close || limitPrice < marketData.Close)) ||
            (order.TradeAction == TradeAction.Sell && (stopPrice < marketData.Close || limitPrice > marketData.Close)))
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(limitPrice, order.Quantity, order.TradeAction);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            order.Timestamp,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        if (FillOccurred != null)
        {
            await FillOccurred(this, fillEvent).ConfigureAwait(false);
        }
        return true;
    }
}

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
    private readonly ITradingCalendar? _tradingCalendar;
    private readonly ILogger<SimulatedBrokerage> _logger;
    private readonly List<Order> _pendingOrders = [];

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerage class with explicit cost, slippage models, and optional trading calendar.
    /// </summary>
    /// <param name="marketDataFetcher">An instance of an object implementing the IMarketDataFetcher interface.</param>
    /// <param name="costModel">The transaction cost model for calculating commissions.</param>
    /// <param name="slippageModel">The slippage model for adjusting fill prices. Defaults to no slippage.</param>
    /// <param name="tradingCalendar">Optional trading calendar for non-trading-day fill warnings.</param>
    /// <param name="logger">Optional logger for structured logging.</param>
    public SimulatedBrokerage(
        IMarketDataFetcher marketDataFetcher,
        ITransactionCostModel costModel,
        ISlippageModel? slippageModel = null,
        ITradingCalendar? tradingCalendar = null,
        ILogger<SimulatedBrokerage>? logger = null)
    {
        Guard.AgainstNull(() => marketDataFetcher);
        Guard.AgainstNull(() => costModel);

        _marketDataFetcher = marketDataFetcher;
        _costModel = costModel;
        _slippageModel = slippageModel ?? new NoSlippage();
        _tradingCalendar = tradingCalendar;
        _logger = logger ?? NullLogger<SimulatedBrokerage>.Instance;
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
    /// Provides pre-buffered market data for backtest mode, eliminating per-order FetchMarketDataAsync calls.
    /// </summary>
    [Obsolete("Buffered market data is not used. ProcessPendingOrdersAsync receives data directly.")]
    public void SetBufferedMarketData(IReadOnlyDictionary<DateOnly, SortedDictionary<Domain.ValueObjects.Asset, MarketData>> data)
    {
        // No-op: retained for API compatibility.
        ArgumentNullException.ThrowIfNull(data);
    }

    /// <summary>
    /// Queues an order for next-bar execution. Orders are not filled immediately — they are
    /// deferred to the next bar's ProcessPendingOrdersAsync call to eliminate look-ahead bias.
    /// In backtest mode, signals generated on bar T produce orders that fill at bar T+1 prices.
    /// </summary>
    /// <param name="order">The order to be queued.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the order was accepted (queued); false if rejected.</returns>
    public Task<bool> SubmitOrderAsync(Order order, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => order);
        cancellationToken.ThrowIfCancellationRequested();

        _pendingOrders.Add(order);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Processes pending orders from the previous bar against the current bar's market data.
    /// Market orders fill at Open. Limit/Stop/StopLimit orders check against the bar's OHLC.
    /// Unfilled orders are dropped (day orders, not GTC).
    /// </summary>
    public async Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Domain.ValueObjects.Asset, MarketData> dayData,
        CancellationToken cancellationToken)
    {
        if (_pendingOrders.Count == 0)
        {
            return;
        }

        if (_tradingCalendar is not null && !_tradingCalendar.IsTradingDay(date))
        {
            _logger.LogWarning("Skipping pending order processing on non-trading day {Date}. Orders will carry over to next trading day.", date);
            return;
        }

        var ordersToProcess = _pendingOrders.ToList();
        _pendingOrders.Clear();

        foreach (var order in ordersToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!dayData.TryGetValue(order.Asset, out var marketData))
            {
                _logger.LogWarning("No market data for {Asset} on {Date} — order dropped", order.Asset, date);
                continue;
            }

            _ = order.OrderType switch
            {
                OrderType.Market => await HandleMarketOrder(date, order, marketData, cancellationToken).ConfigureAwait(false),
                OrderType.Limit => await HandleLimitOrder(date, order, marketData, cancellationToken).ConfigureAwait(false),
                OrderType.Stop => await HandleStopOrder(date, order, marketData, cancellationToken).ConfigureAwait(false),
                OrderType.StopLimit => await HandleStopLimitOrder(date, order, marketData, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(order.OrderType), order.OrderType, "OrderType is out of range"),
            };
        }
    }

    /// <summary>
    /// Market orders fill at the next bar's Open price (eliminates look-ahead bias).
    /// </summary>
    private async Task<bool> HandleMarketOrder(DateOnly fillDate, Order order, MarketData marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var theoreticalPrice = marketData.Open;
        var fillPrice = _slippageModel.CalculateFillPrice(theoreticalPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Limit orders check against the next bar's Low (buy) or High (sell).
    /// Fill price is the limit price (or better).
    /// </summary>
    private async Task<bool> HandleLimitOrder(DateOnly fillDate, Order order, MarketData marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        var fillPrice = _slippageModel.CalculateFillPrice(limitPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Stop orders check against the next bar's High (buy) or Low (sell) for trigger.
    /// Fill price is the stop price.
    /// </summary>
    private async Task<bool> HandleStopOrder(DateOnly fillDate, Order order, MarketData marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (order.PrimaryPrice == null)
        {
            throw new InvalidOperationException("Stop price must be set for a stop order.");
        }

        var stopPrice = order.PrimaryPrice.Value;
        if ((order.TradeAction == TradeAction.Buy && marketData.High < stopPrice) ||
            (order.TradeAction == TradeAction.Sell && marketData.Low > stopPrice))
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(stopPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// StopLimit orders check against the next bar's High/Low for stop trigger
    /// and Close for limit fill.
    /// </summary>
    private async Task<bool> HandleStopLimitOrder(DateOnly fillDate, Order order, MarketData marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (order.PrimaryPrice == null || order.SecondaryPrice == null)
        {
            throw new InvalidOperationException("Stop price and limit price must be set for a stop-limit order.");
        }

        var stopPrice = order.PrimaryPrice.Value;
        var limitPrice = order.SecondaryPrice.Value;

        var stopTriggered = order.TradeAction == TradeAction.Buy
            ? marketData.High >= stopPrice
            : marketData.Low <= stopPrice;

        var limitFilled = order.TradeAction == TradeAction.Buy
            ? marketData.Close <= limitPrice
            : marketData.Close >= limitPrice;

        if (!stopTriggered || !limitFilled)
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(limitPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Asset,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// R2C-04 fix: Iterate GetInvocationList() to await all multicast handlers.
    /// Thread-safe copy-to-local pattern preserved.
    /// </summary>
    private async Task RaiseFillOccurredAsync(FillEvent fillEvent, CancellationToken cancellationToken)
    {
        var handler = FillOccurred;
        if (handler == null)
        {
            return;
        }

        foreach (var d in handler.GetInvocationList().Cast<Func<object, FillEvent, Task>>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await d(this, fillEvent).ConfigureAwait(false);
        }
    }
}

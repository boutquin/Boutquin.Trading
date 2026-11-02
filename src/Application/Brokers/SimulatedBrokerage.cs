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
/// Orders are queued and filled on the next bar via ProcessPendingOrdersAsync.
/// </summary>
public sealed class SimulatedBrokerage : IBrokerage
{
    private readonly ITransactionCostModel _costModel;
    private readonly ISlippageModel _slippageModel;
    private readonly IBusinessCalendar? _tradingCalendar;
    private readonly ILogger<SimulatedBrokerage> _logger;
    private readonly IFxCostModel _fxCostModel;
    private readonly List<Order> _pendingOrders = [];
    private readonly List<FxOrder> _pendingFxOrders = [];
    private readonly Lock _pendingOrdersLock = new();

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerage class with explicit cost, slippage models, and optional trading calendar.
    /// </summary>
    /// <param name="costModel">The transaction cost model for calculating commissions.</param>
    /// <param name="slippageModel">The slippage model for adjusting fill prices. Defaults to no slippage.</param>
    /// <param name="tradingCalendar">Optional trading calendar for non-trading-day fill warnings.</param>
    /// <param name="logger">Optional logger for structured logging.</param>
    /// <param name="fxCostModel">The cost model for FX conversions. Defaults to <see cref="SpreadFxCostModel.Free"/> (no spread).</param>
    public SimulatedBrokerage(
        ITransactionCostModel costModel,
        ISlippageModel? slippageModel = null,
        IBusinessCalendar? tradingCalendar = null,
        ILogger<SimulatedBrokerage>? logger = null,
        IFxCostModel? fxCostModel = null)
    {
        Guard.AgainstNull(() => costModel);

        _costModel = costModel;
        _slippageModel = slippageModel ?? new NoSlippage();
        _fxCostModel = fxCostModel ?? SpreadFxCostModel.Free;
        _tradingCalendar = tradingCalendar;
        _logger = logger ?? NullLogger<SimulatedBrokerage>.Instance;
    }

    /// <summary>
    /// Initializes a new instance of the SimulatedBrokerage class with a flat commission rate.
    /// Backward-compatible constructor that wraps the rate in a <see cref="PercentageOfValueCostModel"/>.
    /// </summary>
    /// <param name="commissionRate">The commission rate to apply to trades (default: 0.1%).</param>
    public SimulatedBrokerage(decimal commissionRate = 0.001m)
        : this(new PercentageOfValueCostModel(commissionRate))
    {
    }

    /// <summary>
    /// Occurs when an order is filled. This can be subscribed to in order to receive fill events.
    /// </summary>
    public event Func<object, FillEvent, Task>? FillOccurred;

    /// <summary>
    /// Occurs when an FX conversion is filled. Subscribe to receive <see cref="FxFillEvent"/>s.
    /// </summary>
    public event Func<object, FxFillEvent, Task>? FxFillOccurred;

    /// <summary>
    /// Provides pre-buffered market data for backtest mode, eliminating per-order FetchMarketDataAsync calls.
    /// </summary>
    [Obsolete("Buffered market data is not used. ProcessPendingOrdersAsync receives data directly.")]
    public void SetBufferedMarketData(IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> data)
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

        // Orders may be submitted concurrently from different strategies/threads
        // in live mode; List<T>.Add is not thread-safe.
        lock (_pendingOrdersLock)
        {
            _pendingOrders.Add(order);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Queues a spot FX conversion for execution on the next bar, between that bar's asset sells
    /// and asset buys.
    /// </summary>
    /// <param name="order">The conversion to queue.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Always <see langword="true"/>: the simulated brokerage accepts every FX order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="order"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="order"/>'s <see cref="FxOrder.BuyAmount"/> is not positive.</exception>
    /// <exception cref="ArgumentException">Thrown when the order sells and buys the same currency.</exception>
    public Task<bool> SubmitFxOrderAsync(FxOrder order, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => order);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order.BuyAmount);
        if (order.SellCurrency == order.BuyCurrency)
        {
            throw new ArgumentException($"FX order sells and buys the same currency ({order.SellCurrency}).", nameof(order));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_pendingOrdersLock)
        {
            _pendingFxOrders.Add(order);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Processes pending asset orders from the previous bar against the current bar's market data.
    /// Market orders fill at Open. Limit/Stop/StopLimit orders check against the bar's OHLC.
    /// Unfilled orders are dropped (day orders, not GTC). Pending FX orders stay queued: without a
    /// base currency and an FX table they cannot be priced — use the FX-aware overload.
    /// </summary>
    public Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        CancellationToken cancellationToken) =>
        ProcessPendingOrdersAsync(date, dayData, baseCurrency: null, fxRates: null, cancellationToken);

    /// <summary>
    /// Processes pending asset and FX orders from the previous bar against the current bar, in the
    /// order <b>asset sells → FX conversions → asset buys</b>: sale proceeds exist before a conversion
    /// sells them on, and converted cash exists before a buy draws on it. Market orders fill at Open;
    /// Limit/Stop/StopLimit orders check against the bar's OHLC; FX orders fill at the bar's resolved
    /// rate. Unfilled orders are dropped (day orders, not GTC).
    /// </summary>
    /// <param name="date">The current bar's date.</param>
    /// <param name="dayData">The current bar's market data for all assets.</param>
    /// <param name="baseCurrency">The portfolio's base currency; the one currency <paramref name="fxRates"/> never keys.</param>
    /// <param name="fxRates">The bar's resolved FX table: base-currency units per 1 unit of each keyed currency.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a pending FX order needs a rate the bar's table does not carry.
    /// </exception>
    public Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        Currency baseCurrency,
        SortedDictionary<Currency, decimal> fxRates,
        CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => fxRates);
        return ProcessPendingOrdersAsync(date, dayData, (Currency?)baseCurrency, fxRates, cancellationToken);
    }

    private async Task ProcessPendingOrdersAsync(
        DateOnly date,
        SortedDictionary<Symbol, Bar> dayData,
        Currency? baseCurrency,
        SortedDictionary<Currency, decimal>? fxRates,
        CancellationToken cancellationToken)
    {
        // Atomically snapshot and reset under the lock; emptiness must be observed
        // against the same synchronized view that the Submit methods write to.
        List<Order> ordersToProcess;
        List<FxOrder> fxOrdersToProcess;
        lock (_pendingOrdersLock)
        {
            var fxProcessable = fxRates is not null && _pendingFxOrders.Count > 0;
            if (_pendingOrders.Count == 0 && !fxProcessable)
            {
                return;
            }

            if (_tradingCalendar is not null && !_tradingCalendar.IsBusinessDay(date))
            {
                _logger.LogWarning("Skipping pending order processing on non-trading day {Date}. Orders will carry over to next trading day.", date);
                return;
            }

            ordersToProcess = [.. _pendingOrders];
            _pendingOrders.Clear();

            fxOrdersToProcess = fxProcessable ? [.. _pendingFxOrders] : [];
            if (fxProcessable)
            {
                _pendingFxOrders.Clear();
            }
        }

        // Sells first, then FX, then buys — each group in submission order. Sells-first is what
        // the rebalancing differ already emits; the FX leg between them is what a base-funded
        // buy of a foreign asset (or a foreign sale that funds a base buy) needs.
        foreach (var order in ordersToProcess.Where(o => o.TradeAction == TradeAction.Sell))
        {
            await ProcessAssetOrderAsync(date, dayData, order, cancellationToken).ConfigureAwait(false);
        }

        foreach (var fxOrder in fxOrdersToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await HandleFxOrder(date, fxOrder, baseCurrency!.Value, fxRates!, cancellationToken).ConfigureAwait(false);
        }

        foreach (var order in ordersToProcess.Where(o => o.TradeAction == TradeAction.Buy))
        {
            await ProcessAssetOrderAsync(date, dayData, order, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessAssetOrderAsync(DateOnly date, SortedDictionary<Symbol, Bar> dayData, Order order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!dayData.TryGetValue(order.Symbol, out var marketData))
        {
            _logger.LogWarning("No market data for {Symbol} on {Date} — order dropped", order.Symbol, date);
            return;
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

    /// <summary>
    /// Fills an FX order at the bar's resolved rate. The table holds base units per 1 unit of each
    /// non-base currency, so the sell-per-buy rate is <c>fx[buy] / fx[sell]</c> with <c>fx[base] = 1</c>.
    /// </summary>
    private async Task HandleFxOrder(
        DateOnly fillDate,
        FxOrder order,
        Currency baseCurrency,
        SortedDictionary<Currency, decimal> fxRates,
        CancellationToken cancellationToken)
    {
        var rate = BaseUnitsPerUnit(order.BuyCurrency) / BaseUnitsPerUnit(order.SellCurrency);
        var sellAmount = order.BuyAmount * rate;
        var commission = _fxCostModel.CalculateCommission(order.SellCurrency, sellAmount, order.BuyCurrency, order.BuyAmount);

        var fillEvent = new FxFillEvent(
            fillDate,
            order.StrategyName,
            order.SellCurrency,
            sellAmount,
            order.BuyCurrency,
            order.BuyAmount,
            rate,
            commission);

        await RaiseFxFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);

        decimal BaseUnitsPerUnit(Currency currency)
        {
            if (currency == baseCurrency)
            {
                return 1m;
            }

            if (fxRates.TryGetValue(currency, out var baseUnits))
            {
                return baseUnits;
            }

            throw new InvalidOperationException(
                $"Cannot fill FX order {order.SellCurrency}→{order.BuyCurrency} for strategy '{order.StrategyName}' on {fillDate:yyyy-MM-dd}: " +
                $"no FX rate for {currency}. The bar's FX table carries [{string.Join(", ", fxRates.Keys)}] against base {baseCurrency}.");
        }
    }

    /// <summary>
    /// Market orders fill at the next bar's adjusted open price (eliminates look-ahead bias).
    /// Uses AdjustedOpen = Open × (AdjustedClose / Close) so that fill prices are consistent
    /// with position sizing and equity valuation, both of which use AdjustedClose.
    /// When AdjustedClose == Close (no dividend adjustment), this reduces to raw Open.
    /// </summary>
    private async Task<bool> HandleMarketOrder(DateOnly fillDate, Order order, Bar marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Derive adjusted open from the same factor used for AdjustedClose.
        // Tiingo provides adjClose but not adjOpen; the per-bar adjustment factor
        // (AdjustedClose / Close) applies equally to all intraday prices.
        var adjFactor = marketData.Close > 0m ? marketData.AdjustedClose / marketData.Close : 1m;
        var theoreticalPrice = marketData.Open * adjFactor;
        var fillPrice = _slippageModel.CalculateFillPrice(theoreticalPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Symbol,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Limit orders check against the next bar's adjusted Low (buy) or adjusted High (sell).
    /// Fill price is the limit price (or better). Uses the same AdjustedClose/Close factor
    /// applied to OHLC so that limit prices expressed in adjusted terms compare correctly.
    /// </summary>
    private async Task<bool> HandleLimitOrder(DateOnly fillDate, Order order, Bar marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (order.PrimaryPrice == null)
        {
            throw new InvalidOperationException("Limit price must be set for a limit order.");
        }

        var limitPrice = order.PrimaryPrice.Value;
        var adjFactor = marketData.Close > 0m ? marketData.AdjustedClose / marketData.Close : 1m;
        var adjLow = marketData.Low * adjFactor;
        var adjHigh = marketData.High * adjFactor;
        if ((order.TradeAction == TradeAction.Buy && adjLow > limitPrice) ||
            (order.TradeAction == TradeAction.Sell && adjHigh < limitPrice))
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(limitPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Symbol,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Stop orders check against the next bar's adjusted High (buy) or adjusted Low (sell) for
    /// trigger. Fill price is the stop price. Uses the same AdjustedClose/Close factor as the
    /// limit path so that stop prices expressed in adjusted terms compare on one basis.
    /// </summary>
    private async Task<bool> HandleStopOrder(DateOnly fillDate, Order order, Bar marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (order.PrimaryPrice == null)
        {
            throw new InvalidOperationException("Stop price must be set for a stop order.");
        }

        var stopPrice = order.PrimaryPrice.Value;
        var adjFactor = marketData.Close > 0m ? marketData.AdjustedClose / marketData.Close : 1m;
        var adjLow = marketData.Low * adjFactor;
        var adjHigh = marketData.High * adjFactor;
        if ((order.TradeAction == TradeAction.Buy && adjHigh < stopPrice) ||
            (order.TradeAction == TradeAction.Sell && adjLow > stopPrice))
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(stopPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Symbol,
            order.StrategyName,
            order.TradeAction,
            fillPrice,
            order.Quantity,
            commission);

        await RaiseFillOccurredAsync(fillEvent, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// StopLimit orders check against the next bar's adjusted High/Low for stop trigger
    /// and adjusted Close for limit fill, all on the AdjustedClose/Close basis used by the
    /// limit path (the adjusted Close is the bar's AdjustedClose whenever Close is positive).
    /// </summary>
    private async Task<bool> HandleStopLimitOrder(DateOnly fillDate, Order order, Bar marketData, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (order.PrimaryPrice == null || order.SecondaryPrice == null)
        {
            throw new InvalidOperationException("Stop price and limit price must be set for a stop-limit order.");
        }

        var stopPrice = order.PrimaryPrice.Value;
        var limitPrice = order.SecondaryPrice.Value;

        var adjFactor = marketData.Close > 0m ? marketData.AdjustedClose / marketData.Close : 1m;
        var adjHigh = marketData.High * adjFactor;
        var adjLow = marketData.Low * adjFactor;
        // Close × (AdjustedClose / Close) is AdjustedClose; take it directly so a limit set
        // exactly at AdjustedClose is not lost to the division's rounding.
        var adjClose = marketData.Close > 0m ? marketData.AdjustedClose : marketData.Close;

        var stopTriggered = order.TradeAction == TradeAction.Buy
            ? adjHigh >= stopPrice
            : adjLow <= stopPrice;

        var limitFilled = order.TradeAction == TradeAction.Buy
            ? adjClose <= limitPrice
            : adjClose >= limitPrice;

        if (!stopTriggered || !limitFilled)
        {
            return false;
        }

        var fillPrice = _slippageModel.CalculateFillPrice(limitPrice, order.Quantity, order.TradeAction, marketData.Volume);
        var commission = _costModel.CalculateCommission(fillPrice, order.Quantity, order.TradeAction);

        var fillEvent = new FillEvent(
            fillDate,
            order.Symbol,
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
    private async Task RaiseFxFillOccurredAsync(FxFillEvent fillEvent, CancellationToken cancellationToken)
    {
        var handler = FxFillOccurred;
        if (handler == null)
        {
            return;
        }

        foreach (var d in handler.GetInvocationList().Cast<Func<object, FxFillEvent, Task>>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await d(this, fillEvent).ConfigureAwait(false);
        }
    }

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

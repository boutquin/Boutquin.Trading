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

namespace Boutquin.Trading.Application.EventHandlers;

/// <summary>
/// The FillEventHandler class is an implementation of the IEventHandler interface that handles FillEvent objects.
/// FillEvent objects represent the filling of an order in the trading system.
/// </summary>
/// <remarks>
/// This class handles FillEvent objects by updating the positions and cash of the strategy that created the order.
/// The IPortfolio object that is passed to the FillEventHandler constructor is used to get the strategy and update its state.
/// 
/// Here is an example of how to use this class:
/// <code>
/// var portfolio = new Portfolio();
/// var fillEventHandler = new FillEventHandler();
/// 
/// var fillEvent = new FillEvent();
/// await fillEventHandler.HandleEventAsync(portfolio, fillEvent);
/// </code>
/// </remarks>
public sealed class FillEventHandler : IEventHandler
{
    private readonly ILogger<FillEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FillEventHandler"/> class (backward-compatible overload).
    /// </summary>
    public FillEventHandler()
        : this(NullLogger<FillEventHandler>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FillEventHandler"/> class with structured logging.
    /// </summary>
    public FillEventHandler(ILogger<FillEventHandler> logger)
    {
        _logger = logger ?? NullLogger<FillEventHandler>.Instance;
    }

    /// <summary>
    /// Handles the provided FillEvent object.
    /// </summary>
    /// <param name="portfolio">The portfolio for which the event is relevant.</param>
    /// <param name="eventObj">The FillEvent object to handle.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when portfolio is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when eventObj is not a FillEvent object.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The HandleEventAsync method updates the positions and cash of the strategy that created the order represented by the FillEvent object.
    /// Buy orders are rejected if the strategy has insufficient cash to cover the trade value plus commission.
    /// The strategy is retrieved from the portfolio that was passed to the FillEventHandler constructor.
    /// </remarks>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var fillEvent = eventObj as FillEvent
            ?? throw new ArgumentException("Event must be of type FillEvent.", nameof(eventObj));

        cancellationToken.ThrowIfCancellationRequested();

        // Call methods on the Portfolio class to perform the necessary actions
        var strategy = portfolio.GetStrategy(fillEvent.StrategyName);
        var assetCurrency = portfolio.GetAssetCurrency(fillEvent.Asset);

        var effectiveQty = fillEvent.Quantity;
        var effectiveCommission = fillEvent.Commission;

        // Quantity-limiting for Buy orders (matches zipline's approach):
        // Reduce fill quantity to what cash can afford rather than rejecting outright.
        if (fillEvent.TradeAction == TradeAction.Buy)
        {
            if (!strategy.Cash.TryGetValue(assetCurrency, out var availableCash))
            {
                throw new InvalidOperationException(
                    $"Asset '{fillEvent.Asset}' uses currency {assetCurrency} but strategy " +
                    $"'{fillEvent.StrategyName}' has no {assetCurrency} cash initialized. " +
                    "All portfolio currencies must be initialized before trading.");
            }
            var commissionPerShare = fillEvent.Quantity > 0
                ? fillEvent.Commission / fillEvent.Quantity
                : 0m;
            var costPerShare = fillEvent.FillPrice + commissionPerShare;

            if (costPerShare > 0)
            {
                var maxAffordableQty = (int)Math.Floor(availableCash / costPerShare);
                if (maxAffordableQty < effectiveQty)
                {
                    effectiveQty = Math.Max(maxAffordableQty, 0);

                    if (effectiveQty <= 0)
                    {
                        _logger.LogWarning(
                            "Rejected buy for {Asset}: cash {AvailableCash:N2} cannot afford any shares at {FillPrice:N2}/share",
                            fillEvent.Asset,
                            availableCash,
                            fillEvent.FillPrice);
                        return; // Zero-quantity fill: no position or cash update
                    }

                    // Recalculate commission proportionally for reduced quantity
                    effectiveCommission = fillEvent.Quantity > 0
                        ? fillEvent.Commission * effectiveQty / fillEvent.Quantity
                        : 0m;

                    _logger.LogWarning(
                        "Quantity-limited buy for {Asset}: requested {RequestedQty}, filled {EffectiveQty} " +
                        "(cash {AvailableCash:N2}, cost/share {CostPerShare:N2})",
                        fillEvent.Asset,
                        fillEvent.Quantity,
                        effectiveQty,
                        availableCash,
                        costPerShare);
                }
            }
        }

        // A3 fix: Branch on TradeAction — Buy deducts, Sell credits
        var tradeValue = fillEvent.FillPrice * effectiveQty;
        var cashDelta = fillEvent.TradeAction == TradeAction.Buy
            ? -(tradeValue + effectiveCommission)      // Buy: deduct cost + commission
            : tradeValue - effectiveCommission;          // Sell: credit proceeds - commission

        // R2C-02 fix: Sell must negate quantity to reduce position
        var positionDelta = fillEvent.TradeAction == TradeAction.Buy
            ? effectiveQty
            : -effectiveQty;
        strategy.UpdatePositions(fillEvent.Asset, positionDelta);

        strategy.UpdateCash(assetCurrency, cashDelta);
    }
}

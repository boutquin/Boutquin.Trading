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
/// Handles <see cref="FxFillEvent"/>s by moving cash between the strategy's currency buckets:
/// <c>SellAmount + Commission</c> leaves the sell bucket and <c>BuyAmount</c> enters the buy bucket,
/// which is created if the strategy has never held that currency. Register it under
/// <c>typeof(FxFillEvent)</c> in the portfolio's handler dictionary.
/// </summary>
/// <remarks>
/// The fill is sized at the signal bar's expected cash flows and priced at the fill bar's rate, so
/// the strategy may hold less of the sell currency than the fill asks for (a sale filled below its
/// signal-bar price, a rate that moved). The handler then clips the conversion to what the sell
/// bucket holds, scaling <c>BuyAmount</c> and <c>Commission</c> down proportionally — the FX
/// counterpart of <see cref="FillEventHandler"/>'s quantity limiting — and drops a fill the bucket
/// cannot fund at all, logging both.
/// </remarks>
public sealed class FxFillEventHandler : IEventHandler
{
    private readonly ILogger<FxFillEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxFillEventHandler"/> class without logging.
    /// </summary>
    public FxFillEventHandler()
        : this(NullLogger<FxFillEventHandler>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxFillEventHandler"/> class with structured logging.
    /// </summary>
    /// <param name="logger">The logger; <see langword="null"/> falls back to a no-op logger.</param>
    public FxFillEventHandler(ILogger<FxFillEventHandler> logger)
    {
        _logger = logger ?? NullLogger<FxFillEventHandler>.Instance;
    }

    /// <summary>
    /// Applies the FX fill carried by <paramref name="eventObj"/> to the strategy's cash buckets.
    /// </summary>
    /// <param name="portfolio">The portfolio owning the strategy.</param>
    /// <param name="eventObj">An <see cref="FxFillEvent"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="portfolio"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventObj"/> is not an <see cref="FxFillEvent"/>.</exception>
    /// <returns>A completed task.</returns>
    public Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var fill = eventObj as FxFillEvent
            ?? throw new ArgumentException("Event must be of type FxFillEvent.", nameof(eventObj));

        cancellationToken.ThrowIfCancellationRequested();

        var strategy = portfolio.GetStrategy(fill.StrategyName);
        var available = strategy.Cash.GetValueOrDefault(fill.SellCurrency, 0m);

        var sellAmount = fill.SellAmount;
        var buyAmount = fill.BuyAmount;
        var commission = fill.Commission;
        var totalCost = sellAmount + commission;

        if (totalCost > available)
        {
            if (available <= 0m)
            {
                _logger.LogWarning(
                    "Rejected FX fill for strategy {Strategy}: no {SellCurrency} cash to buy {BuyAmount:N2} {BuyCurrency}",
                    fill.StrategyName,
                    fill.SellCurrency,
                    fill.BuyAmount,
                    fill.BuyCurrency);
                return Task.CompletedTask;
            }

            // Scale every leg by the same factor so the clipped fill keeps the fill's rate and
            // spread; the residual buy-side shortfall is then quantity-limited by the asset fill.
            var scale = available / totalCost;
            sellAmount *= scale;
            buyAmount *= scale;
            commission *= scale;

            _logger.LogWarning(
                "Clipped FX fill for strategy {Strategy}: {SellCurrency} cash {Available:N2} funds {BuyAmount:N2} of the {RequestedBuyAmount:N2} {BuyCurrency} requested",
                fill.StrategyName,
                fill.SellCurrency,
                available,
                buyAmount,
                fill.BuyAmount,
                fill.BuyCurrency);
        }

        strategy.UpdateCash(fill.SellCurrency, -(sellAmount + commission));
        strategy.UpdateCash(fill.BuyCurrency, buyAmount);

        return Task.CompletedTask;
    }
}

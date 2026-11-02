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
/// Handles <see cref="FxOrderEvent"/>s by submitting them to the portfolio's brokerage, as
/// <see cref="OrderEventHandler"/> does for asset orders. Register it under
/// <c>typeof(FxOrderEvent)</c> in the portfolio's handler dictionary, together with
/// <see cref="FxFillEventHandler"/>, for any portfolio that holds assets outside its funding currency.
/// </summary>
/// <remarks>
/// FX orders bypass the risk manager: <see cref="IRiskRule"/> evaluates asset orders, and a
/// conversion changes neither positions nor total value beyond its own cost.
/// </remarks>
public sealed class FxOrderEventHandler : IEventHandler
{
    private readonly ILogger<FxOrderEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxOrderEventHandler"/> class without logging.
    /// </summary>
    public FxOrderEventHandler()
        : this(NullLogger<FxOrderEventHandler>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FxOrderEventHandler"/> class with structured logging.
    /// </summary>
    /// <param name="logger">The logger; <see langword="null"/> falls back to a no-op logger.</param>
    public FxOrderEventHandler(ILogger<FxOrderEventHandler> logger)
    {
        _logger = logger ?? NullLogger<FxOrderEventHandler>.Instance;
    }

    /// <summary>
    /// Submits the FX order carried by <paramref name="eventObj"/> to the portfolio's brokerage.
    /// </summary>
    /// <param name="portfolio">The portfolio that routes the order.</param>
    /// <param name="eventObj">An <see cref="FxOrderEvent"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="portfolio"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventObj"/> is not an <see cref="FxOrderEvent"/>.</exception>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var fxOrderEvent = eventObj as FxOrderEvent
            ?? throw new ArgumentException("Event must be of type FxOrderEvent.", nameof(eventObj));

        cancellationToken.ThrowIfCancellationRequested();

        var submitted = await portfolio.SubmitFxOrderAsync(fxOrderEvent, cancellationToken).ConfigureAwait(false);
        if (!submitted)
        {
            _logger.LogWarning(
                "FX order submission failed for strategy {Strategy}: sell {SellCurrency} to buy {BuyAmount:N2} {BuyCurrency}",
                fxOrderEvent.StrategyName,
                fxOrderEvent.SellCurrency,
                fxOrderEvent.BuyAmount,
                fxOrderEvent.BuyCurrency);
        }
    }
}

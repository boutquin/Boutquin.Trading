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
/// The OrderEventHandler class is an implementation of the IEventHandler interface that handles OrderEvent objects.
/// OrderEvent objects represent the creation of an order in the trading system.
/// </summary>
/// <remarks>
/// This class handles OrderEvent objects by submitting the order to the portfolio.
/// The IPortfolio object that is passed to the OrderEventHandler constructor is used to submit the order.
/// </remarks>
public sealed class OrderEventHandler : IEventHandler
{
    private readonly ILogger<OrderEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderEventHandler"/> class (backward-compatible overload).
    /// </summary>
    public OrderEventHandler()
        : this(NullLogger<OrderEventHandler>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderEventHandler"/> class with structured logging.
    /// </summary>
    /// <param name="logger">A logger for structured logging.</param>
    public OrderEventHandler(ILogger<OrderEventHandler> logger)
    {
        _logger = logger ?? NullLogger<OrderEventHandler>.Instance;
    }

    /// <summary>
    /// Handles the provided OrderEvent object.
    /// </summary>
    /// <param name="portfolio">The portfolio to submit the order to.</param>
    /// <param name="eventObj">The OrderEvent object to handle.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when portfolio is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when eventObj is not a OrderEvent object.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var orderEvent = eventObj as OrderEvent
            ?? throw new ArgumentException("Event must be of type OrderEvent.", nameof(eventObj));

        cancellationToken.ThrowIfCancellationRequested();

        // M15: Log warning instead of throwing — order rejection is a normal backtest condition
        var orderSubmitted = await portfolio.SubmitOrderAsync(orderEvent, cancellationToken).ConfigureAwait(false);
        if (!orderSubmitted)
        {
            _logger.LogWarning(
                "Order submission failed for {Asset} ({TradeAction} {Quantity} @ {OrderType})",
                orderEvent.Asset,
                orderEvent.TradeAction,
                orderEvent.Quantity,
                orderEvent.OrderType);
        }
    }
}

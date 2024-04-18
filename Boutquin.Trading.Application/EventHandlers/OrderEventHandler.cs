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
namespace Boutquin.Trading.Application.EventHandlers;

using Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// The OrderEventHandler class is an implementation of the IEventHandler interface that handles OrderEvent objects.
/// OrderEvent objects represent the creation of an order in the trading system.
/// </summary>
/// <remarks>
/// This class handles OrderEvent objects by submitting the order to the portfolio.
/// The IPortfolio object that is passed to the OrderEventHandler constructor is used to submit the order.
/// 
/// Here is an example of how to use this class:
/// <code>
/// var portfolio = new Portfolio();
/// var orderEventHandler = new OrderEventHandler();
/// 
/// var orderEvent = new OrderEvent();
/// await orderEventHandler.HandleEventAsync(portfolio, orderEvent);
/// </code>
/// </remarks>
public sealed class OrderEventHandler : IEventHandler
{
    /// <summary>
    /// Handles the provided OrderEvent object.
    /// </summary>
    /// <param name="eventObj">The OrderEvent object to handle.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when portfolio is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when eventObj is not a OrderEvent object.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The HandleEventAsync method submits the order represented by the OrderEvent object to the portfolio.
    /// The portfolio is retrieved from the portfolio that was passed to the OrderEventHandler constructor.
    /// </remarks>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var orderEvent = eventObj as OrderEvent
            ?? throw new ArgumentException("Event must be of type OrderEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        if (await portfolio.SubmitOrderAsync(orderEvent))
        {
            // Log success
        }
        // Log failure
    }
}

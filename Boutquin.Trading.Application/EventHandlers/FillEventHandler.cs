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
    /// <summary>
    /// Handles the provided FillEvent object.
    /// </summary>
    /// <param name="eventObj">The FillEvent object to handle.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when portfolio is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when eventObj is not a FillEvent object.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The HandleEventAsync method updates the positions and cash of the strategy that created the order represented by the FillEvent object.
    /// The strategy is retrieved from the portfolio that was passed to the FillEventHandler constructor.
    /// </remarks>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var fillEvent = eventObj as FillEvent
            ?? throw new ArgumentException("Event must be of type FillEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        var strategy = portfolio.GetStrategy(fillEvent.StrategyName);
        strategy.UpdatePositions(fillEvent.Asset, fillEvent.Quantity);

        var tradeValue = fillEvent.FillPrice * fillEvent.Quantity;
        var tradeCost = tradeValue + fillEvent.Commission;
        strategy.UpdateCash(portfolio.GetAssetCurrency(fillEvent.Asset), -tradeCost);
    }
}

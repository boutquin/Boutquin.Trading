﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
/// The MarketEventHandler class is an implementation of the IEventHandler interface that handles MarketEvent objects.
/// MarketEvent objects represent the market data for a specific financial asset at a specific point in time.
/// </summary>
/// <remarks>
/// This class handles MarketEvent objects by updating the historical data, positions, and cash of the portfolio.
/// The IPortfolio object that is passed to the MarketEventHandler constructor is used to update the portfolio state.
/// 
/// Here is an example of how to use this class:
/// <code>
/// var portfolio = new Portfolio();
/// var marketEventHandler = new MarketEventHandler();
/// 
/// var marketEvent = new MarketEvent();
/// await marketEventHandler.HandleEventAsync(portfolio, marketEvent);
/// </code>
/// </remarks>
public sealed class MarketEventHandler : IEventHandler
{
    /// <summary>
    /// Handles the provided MarketEvent object.
    /// </summary>
    /// <param name="eventObj">The MarketEvent object to handle.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when portfolio is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when eventObj is not a MarketEvent object.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The HandleEventAsync method updates the historical data, positions, and cash of the portfolio based on the MarketEvent object.
    /// The portfolio is retrieved from the portfolio that was passed to the MarketEventHandler constructor.
    /// </remarks>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var marketEvent = eventObj as MarketEvent
            ?? throw new ArgumentException("Event must be of type MarketEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        portfolio.UpdateHistoricalData(marketEvent);

        // Detect and handle dividend and split events
        foreach (var (asset, marketData) in marketEvent.HistoricalMarketData)
        {
            // Detect and handle dividend events
            var dividendPerShare = marketData.DividendPerShare;
            if (dividendPerShare > 0)
            {
                portfolio.UpdateCashForDividend(asset, dividendPerShare);
            }

            // Detect and handle split events
            var splitCoefficient = marketData.SplitCoefficient;
            if (splitCoefficient == 1)
            {
                continue;
            }

            portfolio.AdjustPositionForSplit(asset, splitCoefficient);
            if (portfolio.IsLive)
            {
                portfolio.AdjustHistoricalDataForSplit(asset, splitCoefficient);
            }
        }

        //await _portfolio.AllocateCapitalAsync();

        portfolio.GenerateSignals(marketEvent);
    }
}

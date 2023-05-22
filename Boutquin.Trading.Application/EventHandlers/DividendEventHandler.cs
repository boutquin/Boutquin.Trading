// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

public sealed class DividendEventHandler : IEventHandler
{
    private readonly IPortfolio _portfolio;

    public DividendEventHandler(IPortfolio portfolio)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        _portfolio = portfolio;
    }

    public async Task HandleEventAsync(IEvent eventObj)
    {
        var dividendEvent = eventObj as DividendEvent 
            ?? throw new ArgumentException("Event must be of type DividendEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions

        // Determine the currency of the asset
        var assetCurrency = _portfolio.GetAssetCurrency(dividendEvent.Asset);

        // Iterate through all the strategies
        foreach (var strategyEntry in _portfolio.Strategies)
        {
            var strategy = strategyEntry.Value;

            // Check if the strategy holds the asset in the dividend event
            if (!strategy.Positions.TryGetValue(dividendEvent.Asset, out var positionQuantity))
            {
                continue;
            }

            // Calculate the dividend amount by multiplying the dividend per share with the position quantity
            var dividendAmount = dividendEvent.DividendPerShare * positionQuantity;

            // Update the strategy's cash balance by adding the dividend amount in the native currency
            strategy.UpdateCash(assetCurrency, dividendAmount);
        }
    }
}

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

public sealed class FillEventHandler : IEventHandler
{
    private readonly IPortfolio _portfolio;

    public FillEventHandler(IPortfolio portfolio)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        _portfolio = portfolio;
    }

    public async Task HandleEventAsync(IFinancialEvent eventObj)
    {
        var fillEvent = eventObj as FillEvent 
            ?? throw new ArgumentException("Event must be of type FillEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        var strategy = _portfolio.GetStrategy(fillEvent.StrategyName);
        strategy.UpdatePositions(fillEvent.Asset, fillEvent.Quantity);

        var tradeValue = fillEvent.FillPrice * fillEvent.Quantity;
        var tradeCost = tradeValue + fillEvent.Commission;
        strategy.UpdateCash(_portfolio.GetAssetCurrency(fillEvent.Asset), -tradeCost);
    }
}

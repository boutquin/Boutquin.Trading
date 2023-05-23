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

public sealed class MarketEventHandler : IEventHandler
{
    private readonly IPortfolio _portfolio;
    private readonly CurrencyCode _baseCurrency;

    public MarketEventHandler(IPortfolio portfolio, CurrencyCode baseCurrency)
    {
        // Validate parameters
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException
        Guard.AgainstUndefinedEnumValue(() => baseCurrency); // Throws ArgumentOutOfRangeException

        _portfolio = portfolio;
        _baseCurrency = baseCurrency;
    }

    public async Task HandleEventAsync(IEvent eventObj)
    {
        var marketEvent = eventObj as MarketEvent 
            ?? throw new ArgumentException("Event must be of type MarketEvent.", nameof(eventObj));

        // Call methods on the Portfolio class to perform the necessary actions
        _portfolio.UpdateHistoricalData(marketEvent);

        // Detect and handle dividend and split events
        foreach (var (asset, marketData) in marketEvent.HistoricalMarketData)
        {
            // Detect and handle dividend events
            var dividendPerShare = marketData.DividendPerShare;
            if (dividendPerShare > 0)
            {
                _portfolio.UpdateCashForDividend(asset, dividendPerShare);
            }

            // Detect and handle split events
            var splitCoefficient = marketData.SplitCoefficient;
            if (splitCoefficient == 1)
            {
                continue;
            }

            _portfolio.AdjustPositionForSplit(asset, splitCoefficient);
            if (_portfolio.IsLive)
            {
                _portfolio.AdjustHistoricalDataForSplit(asset, splitCoefficient);
            }
        }

        //await _portfolio.AllocateCapitalAsync();

        _portfolio.GenerateSignals(marketEvent, _baseCurrency);
    }
}

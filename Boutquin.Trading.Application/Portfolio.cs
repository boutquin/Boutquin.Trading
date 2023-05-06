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

namespace Boutquin.Trading.Application;

using Boutquin.Domain.Helpers;
using Domain.Data;
using Domain.Enums;
using Domain.Events;
using Boutquin.Trading.Domain.Interfaces;

public sealed class Portfolio
{
    private readonly SortedDictionary<string, IStrategy> _strategies; // StrategyName-> Strategy
    private readonly ICapitalAllocationStrategy _capitalAllocationStrategy;
    private readonly IBrokerage _broker;
    private readonly CurrencyCode _baseCurrency;
    private readonly SortedDictionary<string, CurrencyCode> _assetCurrencies;
    private readonly SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> _historicalMarketData;
    private readonly SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> _historicalFxConversionRates;

    public Portfolio(
        SortedDictionary<string, IStrategy> strategies,
        ICapitalAllocationStrategy capitalAllocationStrategy,
        IBrokerage broker,
        CurrencyCode baseCurrency,
        SortedDictionary<string, CurrencyCode> assetCurrencies,
        SortedDictionary<DateOnly, SortedDictionary<string, MarketData>> historicalMarketData,
        SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        _strategies = strategies;
        _capitalAllocationStrategy = capitalAllocationStrategy;        
        _broker = broker;
        _baseCurrency = baseCurrency;
        _assetCurrencies = assetCurrencies;
        _historicalMarketData = historicalMarketData;
        _historicalFxConversionRates = historicalFxConversionRates;
    }

    public SortedDictionary<DateOnly, decimal> EquityCurve { get; } = new();

    public async Task HandleEventAsync(IEvent eventObj)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => eventObj); // Throws ArgumentNullException when the eventObj parameter is null

        switch (eventObj)
        {
            case MarketEvent marketEvent:
                await HandleMarketEventAsync(marketEvent);
                break;
            case SignalEvent signalEvent:
                await HandleSignalEventAsync(signalEvent);
                break;
            case OrderEvent orderEvent:
                await HandleOrderEventAsync(orderEvent);
                break;
            case FillEvent fillEvent:
                await HandleFillEventAsync(fillEvent);
                break;
            case RebalancingEvent rebalancingEvent:
                await HandleRebalancingEventAsync(rebalancingEvent);
                break;
            case SplitEvent splitEvent:
                await HandleSplitEventAsync(splitEvent);
                break;
            case DividendEvent dividendEvent:
                await HandleDividendEventAsync(dividendEvent);
                break;
            default:
                throw new NotSupportedException($"Unsupported event type: {eventObj.GetType()}");
        }
    }

    private Task HandleMarketEventAsync(MarketEvent marketEvent)
    {
        // Ensure that the marketEvent is not null.
        Guard.AgainstNull(() => marketEvent); // Throws ArgumentNullException when the marketEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    private Task HandleSignalEventAsync(SignalEvent signalEvent)
    {
        // Ensure that the signalEvent is not null.
        Guard.AgainstNull(() => signalEvent); // Throws ArgumentNullException when the signalEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    private Task HandleOrderEventAsync(OrderEvent orderEvent)
    {
        // Ensure that the orderEvent is not null.
        Guard.AgainstNull(() => orderEvent); // Throws ArgumentNullException when the orderEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    private Task HandleFillEventAsync(FillEvent fillEvent)
    {
        // Ensure that the fillEvent is not null.
        Guard.AgainstNull(() => fillEvent); // Throws ArgumentNullException when the fillEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    private Task HandleRebalancingEventAsync(RebalancingEvent rebalancingEvent)
    {
        // Ensure that the rebalancingEvent is not null.
        Guard.AgainstNull(() => rebalancingEvent); // Throws ArgumentNullException when the rebalancingEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    private Task HandleSplitEventAsync(SplitEvent splitEvent)
    {
        // Ensure that the splitEvent is not null.
        Guard.AgainstNull(() => splitEvent); // Throws ArgumentNullException when the splitEvent parameter is null

        //...
        return Task.CompletedTask;
    }

    private Task HandleDividendEventAsync(DividendEvent dividendEvent)
    {
        // Ensure that the dividendEvent is not null.
        Guard.AgainstNull(() => dividendEvent); // Throws ArgumentNullException when the dividendEvent parameter is null

        //...
        return Task.CompletedTask;
    }
}

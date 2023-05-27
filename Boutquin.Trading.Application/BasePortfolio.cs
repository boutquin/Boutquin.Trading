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

using System;
using System.Collections.Generic;

using Domain.Data;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.Extensions.Logging;

public abstract class BasePortfolio : IPortfolio
{
    private IBrokerage _broker;

    public IEventProcessor EventProcessor { get; set; }

    public IBrokerage Broker
    {
        get => _broker;
        set
        {
            if (_broker != null)
            {
                _broker.FillOccurred -= HandleFillEvent;
            }

            _broker = value;

            if (_broker != null)
            {
                _broker.FillOccurred += HandleFillEvent;
            }
        }
    }

    public bool IsLive { get; protected set; } = false;

    public IReadOnlyDictionary<string, IStrategy> Strategies { get; set; } = new Dictionary<string, IStrategy>();

    public IReadOnlyDictionary<string, CurrencyCode> AssetCurrencies { get; set; } = new Dictionary<string, CurrencyCode>();

    public SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?> HistoricalMarketData { get; set; } = new();

    public SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> HistoricalFxConversionRates { get; set; } = new();

    public SortedDictionary<DateOnly, decimal> EquityCurve { get; } = new();

    private async void HandleFillEvent(object sender, FillEvent fillEvent)
    {
        // Ensure that the @event is not null.
        Guard.AgainstNull(() => fillEvent); // Throws ArgumentNullException when the fillEvent parameter is null

        await EventProcessor.ProcessEventAsync(fillEvent);
    }
}

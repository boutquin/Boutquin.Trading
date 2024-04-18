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

using System.Collections.ObjectModel;

using Boutquin.Trading.Application;
using Boutquin.Trading.Application.EventHandlers;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Boutquin.Trading.Application.PositionSizing;
using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Events;
using Boutquin.Trading.Domain.Interfaces;
using Boutquin.Trading.Sample;
using Boutquin.Trading.Domain.ValueObjects;

const CurrencyCode BaseCurrency = CurrencyCode.USD;
var fixedAssetWeights = new Dictionary<Ticker, decimal> { { new Ticker("SPX"), 1m } };
var assetCurrencies = new Dictionary<Ticker, CurrencyCode> { { new Ticker("SPX"), BaseCurrency } };
var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, BaseCurrency);

var options = Options.Create(new MemoryDistributedCacheOptions());
var dataFetcher = new AlphaVantageFetcher(new MemoryDistributedCache(options));
var broker = new SimulatedBrokerage(dataFetcher);

var handlers = new Dictionary<Type, IEventHandler>
{
    { typeof(OrderEvent), new OrderEventHandler() },
    { typeof(MarketEvent), new MarketEventHandler() },
    { typeof(FillEvent), new FillEventHandler() },
    { typeof(SignalEvent), new SignalEventHandler() }
};

var benchmarkStrategy = new BuyAndHoldStrategy(
    nameof(BuyAndHoldStrategy),
    assetCurrencies,
        new SortedDictionary<CurrencyCode, decimal> { { BaseCurrency, 50000m } },
    new DateOnly(2023, 1, 1),
    new ClosePriceOrderPriceCalculationStrategy(),
    positionSizer
    );
var benchmarkPortfolio = new Portfolio(
    BaseCurrency,
    new ReadOnlyDictionary<string, IStrategy>(
        new Dictionary<string, IStrategy> { { nameof(BuyAndHoldStrategy), benchmarkStrategy } }
    ),
    assetCurrencies,
    handlers,
    broker
);

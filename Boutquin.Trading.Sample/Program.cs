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

// Define the base currency for the trading environment.
const CurrencyCode BaseCurrency = CurrencyCode.USD;
// Initialize fixed asset weights for the strategy. Here, 'SPX' is assigned a weight of 1.
var fixedAssetWeights = new Dictionary<Asset, decimal> { { new Asset("SPX"), 1m } };
// Map each asset to its corresponding currency. In this case, 'SPX' is traded in USD.
var assetCurrencies = new Dictionary<Asset, CurrencyCode> { { new Asset("SPX"), BaseCurrency } };
// Create a position sizer that uses fixed weights for sizing positions.
var positionSizer = new FixedWeightPositionSizer(fixedAssetWeights, BaseCurrency);

// Setup the data fetcher for market data. Here, AlphaVantage is used with a memory cache.
var options = Options.Create(new MemoryDistributedCacheOptions());
var dataFetcher = new AlphaVantageFetcher(new MemoryDistributedCache(options));
// Initialize a simulated brokerage for executing trades and fetching market data.
var broker = new SimulatedBrokerage(dataFetcher);

// Define event handlers for different types of events that the system can handle.
var handlers = new Dictionary<Type, IEventHandler>
{
    { typeof(OrderEvent), new OrderEventHandler() },
    { typeof(MarketEvent), new MarketEventHandler() },
    { typeof(FillEvent), new FillEventHandler() },
    { typeof(SignalEvent), new SignalEventHandler() }
};

// Initialize the trading strategy. Here, a simple Buy and Hold strategy is used.
var benchmarkStrategy = new BuyAndHoldStrategy(
    nameof(BuyAndHoldStrategy),
    assetCurrencies,
    new SortedDictionary<CurrencyCode, decimal> { { BaseCurrency, 50000m } },
    new DateOnly(2023, 1, 1),
    new ClosePriceOrderPriceCalculationStrategy(),
    positionSizer
);
// Create a portfolio that uses the defined strategy, event handlers, and brokerage.
var benchmarkPortfolio = new Portfolio(
    BaseCurrency,
    new ReadOnlyDictionary<string, IStrategy>(
        new Dictionary<string, IStrategy> { { nameof(BuyAndHoldStrategy), benchmarkStrategy } }
    ),
    assetCurrencies,
    handlers,
    broker
);


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

using Boutquin.Trading.Application.Strategies;
using Boutquin.Trading.Domain.Enums;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

var options = Options.Create<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions());
var dataFetcher = new AlphaVantageFetcher(new MemoryDistributedCache(options));
var broker = new SimulatedBrokerage(dataFetcher);

var benchmarkStrategy = new BuyAndHoldStrategy(
    nameof(BuyAndHoldStrategy),
    new Dictionary<string, CurrencyCode> { {"SPX", CurrencyCode.USD} },
        new SortedDictionary<CurrencyCode, decimal> { { CurrencyCode.USD, 50000m } },
    );
//var benchmarkPortfolio = new Portfolio()

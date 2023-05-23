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
namespace Boutquin.Trading.UnitTests.Helpers;

using System;
using System.Collections.Generic;

using Boutquin.Trading.Domain.Data;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Interfaces;

public class TestPortfolio : IPortfolio
{
    public bool IsLive => false;

    public IEventProcessor EventProcessor { get; set; }

    public IBrokerage Broker { get; set; }

    public IReadOnlyDictionary<string, IStrategy> Strategies { get; set; } = new Dictionary<string, IStrategy>();

    public IReadOnlyDictionary<string, CurrencyCode> AssetCurrencies { get; set; } = new Dictionary<string, CurrencyCode>();

    public SortedDictionary<DateOnly, SortedDictionary<string, MarketData>?> HistoricalMarketData { get; set; } = new();

    public SortedDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> HistoricalFxConversionRates { get; set; } = new();

    public SortedDictionary<DateOnly, decimal> EquityCurve { get; } = new ();
}

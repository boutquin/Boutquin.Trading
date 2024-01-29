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
namespace Boutquin.Trading.Tests.UnitTests.Helpers;

using Trading.Domain.Data;
using Trading.Domain.Enums;
using Trading.Domain.Events;
using Trading.Domain.Interfaces;

public class TestStrategy : IStrategy
{
    public string Name { get; set; }
    public SortedDictionary<string, int> Positions { get; set; } = new SortedDictionary<string, int>();
    public IReadOnlyDictionary<string, CurrencyCode> Assets { get; set; } = new Dictionary<string, CurrencyCode>();
    public SortedDictionary<CurrencyCode, decimal> Cash { get; set; } = new SortedDictionary<CurrencyCode, decimal>();
    public IOrderPriceCalculationStrategy OrderPriceCalculationStrategy { get; set; }
    public IPositionSizer PositionSizer { get; set; }

    public SignalEvent GenerateSignals(DateOnly timestamp, CurrencyCode baseCurrency, IReadOnlyDictionary<DateOnly, SortedDictionary<string, MarketData>?> historicalMarketData, IReadOnlyDictionary<DateOnly, SortedDictionary<CurrencyCode, decimal>> historicalFxConversionRates)
    {
        throw new NotImplementedException();
    }
}

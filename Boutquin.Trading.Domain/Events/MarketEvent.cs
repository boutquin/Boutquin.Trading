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
namespace Boutquin.Trading.Domain.Events;

using Data;
using Enums;
using Interfaces;

/// <summary>
/// The MarketEvent record encapsulates the historical market data for multiple assets and historical foreign exchange (FX) conversion rates
/// at a specific point in time, represented by the Timestamp property.
/// </summary>
/// <param name="Timestamp">The timestamp of the market event, represented as a DateOnly object.</param>
/// <param name="HistoricalMarketData">A sorted dictionary containing the historical market data for multiple assets, with asset symbols as keys and MarketData objects as values.</param>
/// <param name="HistoricalFxConversionRates">A sorted dictionary containing the historical foreign exchange (FX) conversion rates for multiple currency pairs, with DateOnly as keys and a SortedDictionary of CurrencyCode and decimal pairs as values.</param>
public record MarketEvent(
    DateOnly Timestamp,
    SortedDictionary<string, MarketData> HistoricalMarketData,
    SortedDictionary<CurrencyCode, decimal> HistoricalFxConversionRates) : IEvent;


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

using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Events;

/// <summary>
/// Represents a market event that contains market data such as opening price, high price,
/// low price, closing price, and trading volume for a specific asset at a given timestamp.
/// </summary>
/// <param name="Timestamp">The timestamp at which the market event occurs.</param>
/// <param name="Asset">The identifier of the asset associated with the market event.</param>
/// <param name="Open">The opening price of the asset at the given timestamp.</param>
/// <param name="High">The highest price of the asset during the market event period.</param>
/// <param name="Low">The lowest price of the asset during the market event period.</param>
/// <param name="Close">The closing price of the asset at the given timestamp.</param>
/// <param name="Volume">The trading volume of the asset during the market event period.</param>
public record MarketEvent(
    DateOnly Timestamp,
    string Asset,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume) : IEvent;

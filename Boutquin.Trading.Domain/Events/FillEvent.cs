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
namespace Boutquin.Trading.Domain.Events;

using ValueObjects;

using Interfaces;

/// <summary>
/// The FillEvent record encapsulates the data points for a fill
/// event resulting from the execution of an order for a specific financial asset at a specific point in time,
/// providing information about the fill price, quantity, commission, asset name, timestamp, and strategy name.
/// </summary>
/// <param name="Timestamp">The timestamp of the fill event,
/// represented as a DateOnly object.
/// </param>
/// <param name="Asset">The name of the financial asset associated
/// with the fill event, represented as a string.
/// </param>
/// <param name="StrategyName">The name of the strategy associated with the fill event,
/// represented as a string.
/// </param>
/// <param name="FillPrice">The price at which the financial asset was filled, represented as a decimal value.
/// </param>
/// <param name="Quantity">The quantity of the financial asset filled, represented as an integer.
/// </param>
/// <param name="Commission">The commission associated with the fill event, represented as a decimal value.
/// </param>
public sealed record FillEvent(
    DateOnly Timestamp,
    Ticker Asset,
    string StrategyName,
    decimal FillPrice,
    int Quantity,
    decimal Commission) : IFinancialEvent;

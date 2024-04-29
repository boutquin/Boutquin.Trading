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

/// <summary>
/// The OrderEvent record encapsulates the data points for an order
/// created by a specific strategy for a specific financial asset at a specific point in time,
/// providing information about the order type, trade action, quantity, asset name, timestamp, and strategy name.
/// </summary>
/// <param name="Timestamp">The timestamp of the order event,
/// represented as a DateOnly object.
/// </param>
/// <param name="StrategyName">The name of the strategy that created the order,
/// represented as a string.
/// </param>
/// <param name="Asset">The name of the financial asset associated
/// with the order event, represented as a string.
/// </param>
/// <param name="TradeAction">The trade action of the order, represented as a TradeAction enum value.
/// </param>
/// <param name="OrderType">The type of the order, represented as an OrderType enum value.
/// </param>
/// <param name="Quantity">The quantity of the financial asset in the order, represented as an integer.
/// </param>
/// <param name="PrimaryPrice">The primary price at which the order should be executed,
/// represented as a decimal value. This parameter is only relevant for
/// Limit, Stop, and StopLimit orders. This represents the primary price at which the order should be executed.
/// </param>
/// <param name="SecondaryPrice">The secondary price at which the order should be executed,
/// represented as a decimal value. This parameter is only relevant for
/// StopLimit orders. This represents the secondary price at which the order should be executed.
/// </param>
public sealed record OrderEvent(
    DateOnly Timestamp,
    string StrategyName,
    Asset Asset,
    TradeAction TradeAction,
    OrderType OrderType,
    int Quantity,
    decimal? PrimaryPrice = null, 
    decimal? SecondaryPrice = null) : IFinancialEvent;

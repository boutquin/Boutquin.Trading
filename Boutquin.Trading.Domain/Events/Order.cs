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

using Enums;

using Interfaces;

/// <summary>
/// The Order record represents a request to execute a trade for a specified
/// asset, with information about the trade action, order type, quantity,
/// and other relevant details.
/// </summary>
/// <param name="Timestamp">The timestamp when the order is created,
/// represented as a DateOnly object.
/// </param>
/// <param name="StrategyName">The name of the strategy that associated
/// with the order, represented as a string.
/// </param>
/// <param name="Asset">The name of the financial asset associated
/// with the order, represented as a string.
/// </param>
/// <param name="TradeAction">The action to be performed in the trade,
/// either as a Buy or Sell action, represented as a TradeAction enum value.
/// </param>
/// <param name="OrderType">The type of the order for executing the trade,
/// either as a Market order, Limit order, Stop order, or StopLimit order,
/// represented as an OrderType enum value.
/// </param>
/// <param name="Quantity">The quantity of the financial asset to be traded,
/// represented as an integer value.
/// </param>
/// <param name="Price">The price at which the order should be executed,
/// represented as a decimal value. This parameter is only relevant for
/// Limit, Stop, and StopLimit orders.
/// </param>
public record Order(
    DateOnly Timestamp,
    string StrategyName,
    string Asset,
    TradeAction TradeAction,
    OrderType OrderType,
    int Quantity,
    decimal? PrimaryPrice = null,
    decimal? SecondaryPrice = null) : IEvent;

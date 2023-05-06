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

using System.ComponentModel;

namespace Boutquin.Trading.Domain.Enums;

/// <summary>
/// The OrderType enum represents the type of an order for executing a trade,
/// either as a Market order, Limit order, Stop order, or StopLimit order.
/// </summary>
public enum OrderType
{
    /// <summary>
    /// A Market order is an order to buy or sell an asset immediately at the
    /// best available price in the market.
    /// </summary>
    [Description("Market Order")]
    Market,

    /// <summary>
    /// A Limit order is an order to buy or sell an asset at a specified price
    /// or better. The order will only be executed if the market reaches the
    /// specified limit price.
    /// </summary>
    [Description("Limit Order")]
    Limit,

    /// <summary>
    /// A Stop order is an order to buy or sell an asset once the market price
    /// reaches a specified stop price. It is usually used to limit losses or
    /// protect profits on existing positions.
    /// </summary>
    [Description("Stop Order")]
    Stop,

    /// <summary>
    /// A StopLimit order is a combination of a Stop order and a Limit order.
    /// Once the stop price is reached, a Limit order is placed to execute the
    /// trade at the specified limit price or better.
    /// </summary>
    [Description("Stop Limit Order")]
    StopLimit
}

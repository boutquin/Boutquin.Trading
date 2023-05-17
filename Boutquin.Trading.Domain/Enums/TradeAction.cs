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

namespace Boutquin.Trading.Domain.Enums;
using System.ComponentModel;

/// <summary>
/// The TradeAction enum represents the action to be performed in a trade,
/// either as a Buy or Sell action.
/// </summary>
public enum TradeAction
{
    /// <summary>
    /// A Buy action indicates an intent to purchase an asset, increasing the
    /// position in the asset.
    /// </summary>
    [Description("Buy Action")]
    Buy,

    /// <summary>
    /// A Sell action indicates an intent to sell an asset, decreasing the
    /// position in the asset.
    /// </summary>
    [Description("Sell Action")]
    Sell
}


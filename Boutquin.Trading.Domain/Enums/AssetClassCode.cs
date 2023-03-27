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
/// Enumerates the asset classes used in the schema.
/// </summary>
public enum AssetClassCode
{
    /// <summary>
    /// Cash or cash equivalents.
    /// </summary>
    [Description("Cash or cash equivalents")]
    Cash,

    /// <summary>
    /// Fixed income securities.
    /// </summary>
    [Description("Fixed income securities")]
    FixedIncome,

    /// <summary>
    /// Equity securities.
    /// </summary>
    [Description("Equity securities")]
    Equity,

    /// <summary>
    /// Real estate.
    /// </summary>
    [Description("Real estate")]
    RealEstate,

    /// <summary>
    /// Commodities.
    /// </summary>
    [Description("Commodities")]
    Commodities,

    /// <summary>
    /// Alternative investments.
    /// </summary>
    [Description("Alternative investments")]
    Alternatives,

    /// <summary>
    /// Cryptocurrencies.
    /// </summary>
    [Description("Cryptocurrencies")]
    Crypto,

    /// <summary>
    /// Other.
    /// </summary>
    [Description("Other")]
    Other
}

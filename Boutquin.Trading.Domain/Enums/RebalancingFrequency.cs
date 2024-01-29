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
namespace Boutquin.Trading.Domain.Enums;

using System.ComponentModel;

/// <summary>
/// The RebalancingFrequency enum represents the frequency at which a
/// strategy should rebalance its assets.
/// </summary>
public enum RebalancingFrequency
{
    /// <summary>
    /// Indicates that the strategy should never rebalance its assets.
    /// </summary>
    [Description("Never")]
    Never = 0,

    /// <summary>
    /// Indicates that the strategy should rebalance its assets on a daily basis.
    /// </summary>
    [Description("Daily")]
    Daily = 365,

    /// <summary>
    /// Indicates that the strategy should rebalance its assets on a weekly basis.
    /// </summary>
    [Description("Weekly")]
    Weekly = 52,

    /// <summary>
    /// Indicates that the strategy should rebalance its assets on a monthly basis.
    /// </summary>
    [Description("Monthly")]
    Monthly = 12,

    /// <summary>
    /// Indicates that the strategy should rebalance its assets on a quarterly basis.
    /// </summary>
    [Description("Quarterly")]
    Quarterly = 4,

    /// <summary>
    /// Indicates that the strategy should rebalance its assets on an annual basis.
    /// </summary>
    [Description("Annually")]
    Annually = 1
}

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
/// The SignalType enum represents the type of a trading signal, either Long,
/// Short, or Exit (i.e. closing a position).
/// </summary>
public enum SignalType
{
    /// <summary>
    /// A Long signal indicates a buy signal, where the strategy
    /// is signaling to purchase the asset.
    /// </summary>
    [Description("Long Signal")]
    Long,

    /// <summary>
    /// A Short signal indicates a sell signal, where the strategy
    /// is signaling to sell the asset.
    /// </summary>
    [Description("Short Signal")]
    Short,

    /// <summary>
    /// An Exit signal indicates a closing of a position, where the
    /// strategy is signaling to exit the position.
    /// </summary>
    [Description("Exit Signal")]
    Exit
}

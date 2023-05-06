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

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// The IPositionSizer interface defines a method for calculating the position size of an asset within a strategy.
/// This interface is used by the IStrategy implementations to determine the number of units to trade for a given asset,
/// based on the target capital allocated to the strategy.
/// </summary>
public interface IPositionSizer
{
    /// <summary>
    /// Calculates the position size for a given asset within a strategy, based on the target capital allocated to the strategy.
    /// </summary>
    /// <param name="asset">A string representing the asset symbol to calculate the position size for.</param>
    /// <param name="targetCapital">A decimal representing the target capital allocated to the strategy.</param>
    /// <returns>An integer representing the position size for the given asset.</returns>
    /// <remarks>
    /// The GetPositionSize method should be implemented by the position sizing strategy to determine the number of units to trade
    /// for a given asset, based on the target capital allocated to the strategy. This method should take into account various factors,
    /// such as the asset's historical performance, volatility, and correlation with other assets, as well as any portfolio-level
    /// constraints or objectives.
    /// </remarks>
    int GetPositionSize(string asset, decimal targetCapital);
}


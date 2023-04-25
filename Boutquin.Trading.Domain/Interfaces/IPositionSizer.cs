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
/// IPositionSizer represents a position sizing strategy for a trading system. It calculates
/// the position size for an asset based on the chosen sizing method (e.g., fixed percentage,
/// volatility-based, etc.).
/// </summary>
public interface IPositionSizer
{
    /// <summary>
    /// Calculates and returns the position size for the given asset based on the implemented
    /// position sizing strategy.
    /// </summary>
    /// <param name="asset">The asset for which the position size is calculated.</param>
    /// <param name="portfolioTotalValue">The current total value of the portfolio.</param>
    /// <returns>The position size for the given asset based on the implemented position sizing strategy.</returns>
    int GetPositionSize(string asset, decimal portfolioTotalValue);
}

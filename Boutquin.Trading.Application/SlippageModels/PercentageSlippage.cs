// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Application.SlippageModels;

/// <summary>
/// Applies slippage as a percentage of the theoretical price.
/// Buy orders pay (1 + percentage) * price; sell orders receive (1 - percentage) * price.
/// </summary>
public sealed class PercentageSlippage : ISlippageModel
{
    private readonly decimal _percentage;

    /// <summary>
    /// Initializes a new instance of the <see cref="PercentageSlippage"/> class.
    /// </summary>
    /// <param name="percentage">The slippage percentage as a decimal (e.g., 0.001 for 0.1%).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percentage"/> is negative or zero.</exception>
    public PercentageSlippage(decimal percentage)
    {
        Guard.AgainstNegativeOrZero(() => percentage);
        _percentage = percentage;
    }

    /// <inheritdoc />
    public decimal CalculateFillPrice(decimal theoreticalPrice, int quantity, TradeAction tradeAction) =>
        tradeAction == TradeAction.Buy
            ? theoreticalPrice * (1m + _percentage)
            : theoreticalPrice * (1m - _percentage);
}

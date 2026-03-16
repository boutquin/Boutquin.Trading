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

namespace Boutquin.Trading.Application.RiskManagement;

using Domain.ValueObjects;

/// <summary>
/// Rejects new orders when the portfolio's current drawdown exceeds a configured limit.
/// </summary>
public sealed class MaxDrawdownRule : IRiskRule
{
    private readonly decimal _maxDrawdownPercent;

    /// <summary>
    /// Initializes a new instance of <see cref="MaxDrawdownRule"/>.
    /// </summary>
    /// <param name="maxDrawdownPercent">
    /// The maximum allowable drawdown as a positive decimal (e.g., 0.20 for 20%).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxDrawdownPercent"/> is not between 0 (exclusive) and 1 (inclusive).
    /// </exception>
    public MaxDrawdownRule(decimal maxDrawdownPercent)
    {
        if (maxDrawdownPercent is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDrawdownPercent),
                maxDrawdownPercent,
                "Max drawdown percent must be between 0 (exclusive) and 1 (inclusive).");
        }

        _maxDrawdownPercent = maxDrawdownPercent;
    }

    /// <inheritdoc />
    public string Name => "MaxDrawdown";

    /// <inheritdoc />
    public RiskEvaluation Evaluate(Order order, IPortfolio portfolio)
    {
        Guard.AgainstNull(() => order);
        Guard.AgainstNull(() => portfolio);

        var equityCurve = portfolio.EquityCurve;
        if (equityCurve.Count < 2)
        {
            return RiskEvaluation.Allowed;
        }

        var peak = decimal.MinValue;
        var currentDrawdown = 0m;

        foreach (var value in equityCurve.Values)
        {
            if (value > peak)
            {
                peak = value;
            }

            if (peak > 0)
            {
                var drawdown = (peak - value) / peak;
                if (drawdown > currentDrawdown)
                {
                    currentDrawdown = drawdown;
                }
            }
        }

        if (currentDrawdown > _maxDrawdownPercent)
        {
            return RiskEvaluation.Rejected(
                $"Current drawdown {currentDrawdown:P2} exceeds maximum allowed {_maxDrawdownPercent:P2}.");
        }

        return RiskEvaluation.Allowed;
    }
}

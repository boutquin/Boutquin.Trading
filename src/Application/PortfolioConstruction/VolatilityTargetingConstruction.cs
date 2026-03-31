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

namespace Boutquin.Trading.Application.PortfolioConstruction;

using Boutquin.Trading.Domain.Exceptions;
using Domain.ValueObjects;

/// <summary>
/// Scales a base construction model's weights so that the portfolio's expected volatility
/// matches a target level. If realized vol is higher than target, weights are scaled down;
/// if lower, scaled up (capped at a maximum leverage ratio).
/// </summary>
public sealed class VolatilityTargetingConstruction : ILeveragedConstructionModel
{
    private const int DefaultTradingDaysPerYear = 252;
    private readonly IPortfolioConstructionModel _baseModel;
    private readonly decimal _targetVolatility;
    private readonly decimal _maxLeverage;
    private readonly int _tradingDaysPerYear;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityTargetingConstruction"/> class.
    /// </summary>
    /// <param name="baseModel">The base construction model whose weights are scaled.</param>
    /// <param name="targetVolatility">Target annualized portfolio volatility (e.g., 0.10 for 10%).</param>
    /// <param name="maxLeverage">Maximum leverage ratio (e.g., 1.5 = 150% exposure). Default 1.0 (no leverage).</param>
    /// <param name="tradingDaysPerYear">Trading days per year for annualization. Default 252.</param>
    public VolatilityTargetingConstruction(
        IPortfolioConstructionModel baseModel,
        decimal targetVolatility,
        decimal maxLeverage = 1.0m,
        int tradingDaysPerYear = DefaultTradingDaysPerYear)
    {
        Guard.AgainstNull(() => baseModel);

        if (targetVolatility <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(targetVolatility), "Target volatility must be positive.");
        }

        if (maxLeverage <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLeverage), "Max leverage must be positive.");
        }

        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);

        _baseModel = baseModel;
        _targetVolatility = targetVolatility;
        _maxLeverage = maxLeverage;
        _tradingDaysPerYear = tradingDaysPerYear;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        if (assets.Count == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        var baseWeights = _baseModel.ComputeTargetWeights(assets, returns);

        // Validate base model returned weights for all input assets
        foreach (var asset in assets)
        {
            if (!baseWeights.ContainsKey(asset))
            {
                throw new InvalidOperationException(
                    $"Base construction model did not return a weight for asset '{asset}'. " +
                    "All input assets must have weights; a missing asset would silently receive zero weight.");
            }
        }

        // Compute realized portfolio volatility from weighted returns
        if (returns.Length == 0 || returns.Any(r => r.Length < 2))
        {
            return baseWeights; // Not enough data to estimate vol — use base weights
        }

        var minLength = returns.Min(r => r.Length);
        var portfolioReturns = new decimal[minLength];

        for (var t = 0; t < minLength; t++)
        {
            var dayReturn = 0m;
            for (var i = 0; i < assets.Count; i++)
            {
                var w = baseWeights[assets[i]];
                dayReturn += w * returns[i][t];
            }

            portfolioReturns[t] = dayReturn;
        }

        var mean = portfolioReturns.Average();
        var sumSqDev = portfolioReturns.Sum(r => (r - mean) * (r - mean));
        var dailyVol = (decimal)Math.Sqrt((double)(sumSqDev / (portfolioReturns.Length - 1)));
        var annualizedVol = dailyVol * (decimal)Math.Sqrt(_tradingDaysPerYear);

        if (annualizedVol == 0m)
        {
            throw new CalculationException("Realized portfolio volatility is zero; cannot apply volatility targeting.");
        }

        var scaleFactor = Math.Min(_targetVolatility / annualizedVol, _maxLeverage);

        var scaled = new Dictionary<Asset, decimal>();
        foreach (var asset in assets)
        {
            scaled[asset] = baseWeights[asset] * scaleFactor;
        }

        return scaled;
    }
}

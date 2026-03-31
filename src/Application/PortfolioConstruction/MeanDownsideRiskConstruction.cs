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

using Domain.ValueObjects;
using DownsideRisk;

/// <summary>
/// Generic downside-risk-aware portfolio construction model that maximizes:
///
///   Objective(w) = w'μ − λ × RiskMeasure(w)
///
/// where RiskMeasure is a pluggable <see cref="IDownsideRiskMeasure"/>.
/// Uses projected gradient ascent with line search and simplex projection.
///
/// <para><b>Built-in risk measures:</b></para>
/// <list type="bullet">
///   <item>
///     <see cref="CVaRRiskMeasure"/> — Conditional Value-at-Risk (Rockafellar-Uryasev 2000).
///     Penalizes expected loss in the tail (e.g., worst 5% at α=0.95). Equivalent to
///     maximizing E[r] − λ × CVaR₉₅.
///   </item>
///   <item>
///     <see cref="DownsideDeviationRiskMeasure"/> — Downside deviation below a minimum
///     acceptable return (MAR). When λ=1 and MAR=0, equivalent to maximizing the Sortino ratio.
///   </item>
/// </list>
///
/// Unlike mean-variance optimization which penalizes all volatility symmetrically,
/// downside risk measures only penalize losses, naturally tolerating upside volatility
/// and allocating more to positively-skewed assets.
/// </summary>
public sealed class MeanDownsideRiskConstruction : IPortfolioConstructionModel
{
    private readonly IDownsideRiskMeasure _riskMeasure;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly decimal _riskAversion;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeanDownsideRiskConstruction"/> class.
    /// </summary>
    /// <param name="riskMeasure">The downside risk measure to optimize against.</param>
    /// <param name="minWeight">Minimum weight per asset. Default 0 (long-only).</param>
    /// <param name="maxWeight">Maximum weight per asset. Default 1.0 (no cap).</param>
    /// <param name="riskAversion">Risk aversion parameter λ. Higher values penalize risk more. Default 1.0.</param>
    /// <param name="maxIterations">Maximum optimization iterations. Default 5000.</param>
    /// <param name="tolerance">Convergence tolerance. Default 1e-12.</param>
    public MeanDownsideRiskConstruction(
        IDownsideRiskMeasure riskMeasure,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        decimal riskAversion = 1.0m,
        int maxIterations = 5000,
        decimal tolerance = 1e-12m)
    {
        Guard.AgainstNull(() => riskMeasure);

        _riskMeasure = riskMeasure;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _riskAversion = riskAversion;
        _maxIterations = maxIterations;
        _tolerance = tolerance;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Asset, decimal> ComputeTargetWeights(
        IReadOnlyList<Asset> assets,
        decimal[][] returns)
    {
        Guard.AgainstNull(() => assets);

        if (assets.Count == 0)
        {
            return new Dictionary<Asset, decimal>();
        }

        if (returns is null || returns.Length != assets.Count)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        var n = assets.Count;
        var s = returns[0].Length;

        if (s < 2)
        {
            throw new ArgumentException("At least 2 return observations are required.", nameof(returns));
        }

        // Compute mean returns
        var means = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            means[i] = returns[i].Average();
        }

        // Transpose returns for scenario-based access: scenarios[t][asset]
        var scenarios = new decimal[s][];
        for (var t = 0; t < s; t++)
        {
            scenarios[t] = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                scenarios[t][i] = returns[i][t];
            }
        }

        // Initialize with equal weights
        var w = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = 1m / n;
        }

        var learningRate = 1.0m;
        var converged = false;

        for (var iter = 0; iter < _maxIterations && !converged; iter++)
        {
            // Evaluate current objective: w'μ − λ × Risk(w)
            var portReturn = ComputePortfolioReturn(w, means);
            var (riskValue, riskGrad) = _riskMeasure.Evaluate(w, scenarios, learningRate);
            var objective = portReturn - _riskAversion * riskValue;

            // Gradient of objective: μ − λ × ∂Risk/∂w
            var grad = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                grad[i] = means[i] - _riskAversion * riskGrad[i];
            }

            // Line search: try decreasing step sizes
            var stepped = false;
            var currentLr = learningRate;

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    candidate[i] = w[i] + currentLr * grad[i];
                }

                // Project onto simplex
                ProjectOntoSimplex(candidate, _minWeight, _maxWeight);

                var newPortReturn = ComputePortfolioReturn(candidate, means);

                // Pass learningRate=0 so stateful risk measures (e.g., CVaR's ζ)
                // do not mutate auxiliary state during exploratory line-search
                // evaluations. State should only update at the committed point
                // (the Evaluate call at the top of each iteration).
                var (newRiskValue, _) = _riskMeasure.Evaluate(candidate, scenarios, 0m);
                var newObjective = newPortReturn - _riskAversion * newRiskValue;

                if (newObjective > objective)
                {
                    var maxDiff = 0m;
                    for (var i = 0; i < n; i++)
                    {
                        maxDiff = Math.Max(maxDiff, Math.Abs(candidate[i] - w[i]));
                    }

                    w = candidate;
                    stepped = true;
                    converged = maxDiff < _tolerance;
                    break;
                }

                currentLr *= 0.5m;
            }

            if (!stepped)
            {
                break;
            }
        }

        var weights = new Dictionary<Asset, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            weights[assets[i]] = w[i];
        }

        return weights;
    }

    private static decimal ComputePortfolioReturn(decimal[] w, decimal[] means)
    {
        var result = 0m;
        for (var i = 0; i < w.Length; i++)
        {
            result += w[i] * means[i];
        }

        return result;
    }

    /// <summary>
    /// Projects weights onto the constrained simplex: minWeight ≤ w_i ≤ maxWeight, Σw_i = 1.
    /// Uses iterative clamping and renormalization.
    /// </summary>
    private static void ProjectOntoSimplex(decimal[] w, decimal minWeight, decimal maxWeight)
    {
        var n = w.Length;

        // Auto-relax constraints when infeasible: with N assets, maxWeight must be >= 1/N
        // and minWeight must be <= 1/N, otherwise weights can't sum to 1.0.
        maxWeight = Math.Max(maxWeight, 1m / n);
        minWeight = Math.Min(minWeight, 1m / n);

        for (var round = 0; round < 50; round++)
        {
            // Clamp to [minWeight, maxWeight]
            for (var i = 0; i < n; i++)
            {
                w[i] = Math.Max(minWeight, Math.Min(maxWeight, w[i]));
            }

            // Normalize
            var sum = w.Sum();
            if (sum <= 0m)
            {
                for (var i = 0; i < n; i++)
                {
                    w[i] = 1m / n;
                }

                return;
            }

            for (var i = 0; i < n; i++)
            {
                w[i] /= sum;
            }

            // Check if all constraints satisfied
            var allSatisfied = true;
            for (var i = 0; i < n; i++)
            {
                if (w[i] < minWeight - 1e-14m || w[i] > maxWeight + 1e-14m)
                {
                    allSatisfied = false;
                    break;
                }
            }

            if (allSatisfied)
            {
                return;
            }
        }
    }
}

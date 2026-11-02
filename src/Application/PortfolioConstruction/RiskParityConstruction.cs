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

using Boutquin.Numerics.LinearAlgebra;
using Boutquin.Trading.Application.CovarianceEstimators;
using CalculationException = Boutquin.Trading.Domain.Exceptions.CalculationException;

/// <summary>
/// Computes equal-risk-contribution weights, or constrained risk-budgeting weights when
/// the configured bounds bind. Invalid covariance and nonconvergence fail explicitly.
/// </summary>
/// <remarks>
/// <para><b>Objective:</b> minimizes <c>x'Σx/2 - c Σ log(x_i)</c> over the configured box,
/// choosing positive <c>c</c> so weights sum to one. Free assets have equal variance
/// contributions; assets at a bound need not. This is the box-constrained volatility
/// risk-budgeting formulation with its multiplier rescaled by portfolio volatility.</para>
/// <para><b>Algorithm:</b> exact cyclic coordinate minimization with bisection on <c>c</c>.
/// Griveau-Billion, Richard and Roncalli (2013), arXiv:1311.4057; Richard and Roncalli
/// (2019), arXiv:1902.05710, section 3.2. No expected-return objective is introduced.</para>
/// <para><b>Numerics:</b> decimal arithmetic, uniform covariance scaling, and a relative
/// contribution KKT residual. PSD singular matrices are allowed if a positive-risk solution
/// converges; a zero-risk hedge is not ERC. Bounds are never relaxed. Validation costs
/// O(N³); each coordinate sweep costs O(N²). Storage is O(N²).</para>
/// <para><b>Thread safety:</b> all solver state is per call; concurrent use also requires
/// the injected covariance estimator to support concurrent calls.</para>
/// </remarks>
public sealed class RiskParityConstruction : IPortfolioConstructionModel
{
    private readonly ICovarianceEstimator _covarianceEstimator;
    private readonly decimal _minWeight;
    private readonly decimal _maxWeight;
    private readonly int _maxIterations;
    private readonly decimal _tolerance;

    /// <summary>
    /// Initializes an equal-budget risk allocator with hard per-asset weight bounds.
    /// </summary>
    /// <param name="covarianceEstimator">Covariance estimator; defaults to sample covariance.</param>
    /// <param name="minWeight">Inclusive per-asset floor in [0, 1]. Default zero.</param>
    /// <param name="maxWeight">Inclusive per-asset cap in (0, 1], at least the floor. Default one.</param>
    /// <param name="maxIterations">Positive limit on each inner coordinate solve and on the outer
    /// multiplier bisection. At most its square coordinate sweeps are performed. Default 100.</param>
    /// <param name="tolerance">Positive relative contribution optimality tolerance, less than one.
    /// Default 1e-10. No minimum tolerance is substituted for the caller's value.</param>
    /// <exception cref="ArgumentOutOfRangeException">Bounds, iteration limit or tolerance are invalid.</exception>
    public RiskParityConstruction(
        ICovarianceEstimator? covarianceEstimator = null,
        decimal minWeight = 0m,
        decimal maxWeight = 1.0m,
        int maxIterations = 100,
        decimal tolerance = 1e-10m)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minWeight, 0m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minWeight, 1m);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxWeight, 0m);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxWeight, 1m);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWeight, minWeight);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxIterations, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tolerance, 0m);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tolerance, 1m);
        _covarianceEstimator = covarianceEstimator ?? new SampleCovarianceEstimator();
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _maxIterations = maxIterations;
        _tolerance = tolerance;
    }

    /// <summary>
    /// Allocates fully invested positive weights within the original bounds, checking
    /// constrained optimality before returning. An empty universe returns an empty dictionary.
    /// </summary>
    /// <param name="assets">Distinct symbols in return-series order.</param>
    /// <param name="returns">One aligned series per asset, each containing at least two observations.</param>
    /// <returns>Weights summing to one; equal risk contributions when no bound binds.</returns>
    /// <exception cref="ArgumentNullException">Assets are null.</exception>
    /// <exception cref="ArgumentException">Symbols repeat, returns are malformed, or the box is infeasible.</exception>
    /// <exception cref="CalculationException">Covariance is malformed, asymmetric, indefinite,
    /// has a nonpositive diagonal, or a positive-risk solution does not converge within the limits.
    /// Unrepresentable decimal intermediate results also fail with this exception.</exception>
    public IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeights(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns)
    {
        Guard.AgainstNull(() => assets);
        if (assets.Count == 0)
        {
            return new Dictionary<Symbol, decimal>();
        }

        if (returns is null || returns.Length != assets.Count || assets.Distinct().Count() != assets.Count)
        {
            throw new ArgumentException("Distinct assets and one return series per asset are required.", nameof(returns));
        }

        SampleCovarianceEstimator.ValidateReturns(returns);
        var n = assets.Count;
        if (n * _minWeight > 1m || n * _maxWeight < 1m)
        {
            throw new ArgumentException("Weight bounds are infeasible for this asset count; bounds are not relaxed.");
        }

        decimal[] weights;
        try
        {
            var covariance = ValidateAndScale(_covarianceEstimator.Estimate(returns), n);
            weights = Solve(covariance, n);
        }
        catch (OverflowException exception)
        {
            throw new CalculationException("Risk-budget calculation exceeded decimal range.", exception);
        }

        var result = new Dictionary<Symbol, decimal>(n);
        for (var i = 0; i < n; i++)
        {
            result.Add(assets[i], weights[i]);
        }

        return result;
    }

    private static decimal[,] ValidateAndScale(decimal[,] covariance, int n)
    {
        if (covariance is null || covariance.GetLength(0) != n || covariance.GetLength(1) != n)
        {
            throw new CalculationException("Covariance dimensions must match the asset count.");
        }

        var scale = 0m;
        for (var i = 0; i < n; i++)
        {
            if (covariance[i, i] <= 0m)
            {
                throw new CalculationException("Risk budgeting requires positive asset variances.");
            }

            for (var j = 0; j < n; j++)
            {
                if (covariance[i, j] != covariance[j, i])
                {
                    throw new CalculationException("Covariance must be symmetric.");
                }

                scale = Math.Max(scale, Math.Abs(covariance[i, j]));
            }
        }

        var scaled = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                scaled[i, j] = covariance[i, j] / scale;
            }

            if (scaled[i, i] == 0m)
            {
                throw new CalculationException("Covariance dynamic range exceeds decimal precision.");
            }
        }

        // Eigenvalues are a domain check only: no projection, ridge or eigenvalue clipping.
        // The fixed validation allowance covers decimal eigensolver roundoff, not optimizer error.
        try
        {
            // Congruence to correlation tests even tiny-variance assets on their own scale.
            var correlation = new decimal[n, n];
            var volatility = Enumerable.Range(0, n).Select(i => SquareRoot(scaled[i, i])).ToArray();
            for (var i = 0; i < n; i++)
            {
                correlation[i, i] = 1m;
                for (var j = i + 1; j < n; j++)
                {
                    var value = scaled[i, j] / volatility[i] / volatility[j];
                    if (Math.Abs(value) > 1m + 1e-24m)
                    {
                        throw new CalculationException("Covariance violates the correlation bounds.");
                    }

                    correlation[i, j] = value;
                    correlation[j, i] = value;
                }
            }

            var eigenvalues = JacobiEigenDecomposition<decimal>.Decompose(correlation).Values;
            if (eigenvalues.Any(value => value < -1e-24m * n))
            {
                throw new CalculationException("Covariance must be positive semidefinite.");
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new CalculationException("Covariance validation did not converge.", exception);
        }

        return scaled;
    }

    private decimal[] Solve(decimal[,] covariance, int n)
    {
        var weights = Enumerable.Repeat(1m / n, n).ToArray();
        if (n * _minWeight == 1m || n * _maxWeight == 1m || n == 1)
        {
            BalanceSum(weights);
            RequirePositiveVariance(covariance, weights);
            return weights;
        }

        // Entries are at most one in magnitude and the cap is at most one.
        // At c=n every coordinate's derivative at the upper bound is nonpositive.
        var low = 0m;
        var high = (decimal)n;
        var sumTolerance = _tolerance / 100m;
        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            var budget = (low + high) / 2m;
            if (budget == 0m || budget == low || budget == high)
            {
                break;
            }

            SolveCoordinates(covariance, weights, budget);
            var sum = weights.Sum();
            if (Math.Abs(sum - 1m) <= sumTolerance)
            {
                BalanceSum(weights);
                if (OptimalityResidual(covariance, weights, budget) <= _tolerance)
                {
                    return weights;
                }
            }

            if (sum < 1m)
            {
                low = budget;
            }
            else
            {
                high = budget;
            }
        }

        throw new CalculationException($"Risk-budget multiplier did not converge in {_maxIterations} iterations; no weights returned.");
    }

    private void SolveCoordinates(decimal[,] covariance, decimal[] weights, decimal budget)
    {
        for (var sweep = 0; sweep < _maxIterations; sweep++)
        {
            for (var i = 0; i < weights.Length; i++)
            {
                var cross = 0m;
                for (var j = 0; j < weights.Length; j++)
                {
                    if (j != i)
                    {
                        cross += covariance[i, j] * weights[j];
                    }
                }

                var diagonal = covariance[i, i];
                var productRoot = 2m * SquareRoot(diagonal) * SquareRoot(budget);
                var rootScale = Math.Max(Math.Abs(cross), productRoot);
                if (rootScale == 0m)
                {
                    throw new CalculationException("Risk-budget coordinate is below decimal resolution.");
                }

                var scaledCross = cross / rootScale;
                var scaledProduct = productRoot / rootScale;
                var root = rootScale * SquareRoot(scaledCross * scaledCross + scaledProduct * scaledProduct);
                // Rationalize the positive root when subtraction would cancel.
                var coordinate = cross >= 0m
                    ? 2m * budget / (root + cross)
                    : (root - cross) / (2m * diagonal);
                weights[i] = Math.Clamp(coordinate, _minWeight, _maxWeight);
            }

            if (OptimalityResidual(covariance, weights, budget) <= _tolerance / 1000m)
            {
                return;
            }
        }

        throw new CalculationException($"Risk-budget coordinate descent did not converge in {_maxIterations} sweeps; no weights returned.");
    }

    private decimal OptimalityResidual(decimal[,] covariance, decimal[] weights, decimal budget)
    {
        var variance = RequirePositiveVariance(covariance, weights);
        var residual = 0m;
        for (var i = 0; i < weights.Length; i++)
        {
            var marginal = 0m;
            for (var j = 0; j < weights.Length; j++)
            {
                marginal += covariance[i, j] * weights[j];
            }

            var error = weights[i] * marginal - budget;
            var violation = weights[i] == _minWeight ? Math.Max(0m, -error)
                : weights[i] == _maxWeight ? Math.Max(0m, error) : Math.Abs(error);
            residual = Math.Max(residual, violation / variance);
        }

        return residual;
    }

    private static decimal RequirePositiveVariance(decimal[,] covariance, decimal[] weights)
    {
        var variance = 0m;
        for (var i = 0; i < weights.Length; i++)
        {
            var marginal = 0m;
            for (var j = 0; j < weights.Length; j++)
            {
                marginal += covariance[i, j] * weights[j];
            }

            variance += weights[i] * marginal;
        }

        if (variance <= 0m)
        {
            throw new CalculationException("A zero-risk or negative-variance portfolio cannot define risk budgets.");
        }

        return variance;
    }

    private void BalanceSum(decimal[] weights)
    {
        var remainder = 1m - weights.Sum();
        for (var i = 0; i < weights.Length && remainder != 0m; i++)
        {
            var change = Math.Clamp(remainder, _minWeight - weights[i], _maxWeight - weights[i]);
            weights[i] += change;
            remainder = 1m - weights.Sum();
        }

        if (remainder != 0m || weights.Any(weight => weight <= 0m || weight < _minWeight || weight > _maxWeight))
        {
            throw new CalculationException("Cannot represent fully invested positive weights within the configured bounds.");
        }
    }

    private static decimal SquareRoot(decimal value)
    {
        if (value == 0m)
        {
            return 0m;
        }

        var root = (decimal)Math.Sqrt((double)value);
        for (var i = 0; i < 16; i++)
        {
            var next = (root + value / root) / 2m;
            if (next == root)
            {
                return root;
            }

            root = next;
        }

        return root;
    }
}

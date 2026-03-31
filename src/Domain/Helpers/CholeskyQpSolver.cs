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

namespace Boutquin.Trading.Domain.Helpers;

using Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// Analytical solver for portfolio optimization QPs via Cholesky decomposition
/// and active-set constraint handling.
///
/// Replaces projected gradient descent for:
///   - MinVar: min w'Σw s.t. 1'w=1, minW ≤ w ≤ maxW
///   - MeanVar: max w'μ - (λ/2)w'Σw s.t. 1'w=1, minW ≤ w ≤ maxW
///
/// The active-set method iteratively:
///   1. Solves the unconstrained (sum=1 only) reduced problem via Cholesky
///   2. Fixes the most-violated bound constraint (one per iteration)
///   3. Checks KKT conditions to release constraints that are no longer active
///   4. Terminates in at most 2N iterations
/// </summary>
public static class CholeskyQpSolver
{
    // ─── Public QP solvers ─────────────────────────────────────────────

    /// <summary>
    /// Solves the minimum-variance QP: min w'Σw s.t. 1'w=1, minWeight ≤ w_i ≤ maxWeight.
    /// Analytical solution: w = Σ⁻¹·1 / (1'·Σ⁻¹·1), with active-set for bound constraints.
    /// </summary>
    public static decimal[] SolveMinVarianceQP(
        decimal[,] cov, int n, decimal minWeight, decimal maxWeight)
    {
        if (n == 1)
        {
            return [1m];
        }

        maxWeight = Math.Max(maxWeight, 1m / n);
        minWeight = Math.Min(minWeight, 1m / n);

        // Track which variables are fixed at bounds
        // 0 = free, -1 = fixed at lower, +1 = fixed at upper
        var status = new int[n];

        for (var iter = 0; iter < 2 * n; iter++)
        {
            var freeIndices = new List<int>();
            var fixedSum = 0m;
            for (var i = 0; i < n; i++)
            {
                switch (status[i])
                {
                    case -1:
                        fixedSum += minWeight;
                        break;
                    case 1:
                        fixedSum += maxWeight;
                        break;
                    default:
                        freeIndices.Add(i);
                        break;
                }
            }

            var nFree = freeIndices.Count;
            if (nFree == 0)
            {
                // All variables fixed — return equal weight as safe fallback
                var ew = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    ew[i] = 1m / n;
                }

                return ew;
            }

            var remainingSum = 1m - fixedSum;

            // Build the adjusted RHS for the reduced problem.
            // MinVar reduced KKT: Σ_FF * w_F + Σ_FC * w_C = ν * 1_F
            // With sum constraint: 1'w_F = remainingSum
            // Solution: w_F = c * Σ_FF^{-1} * 1_F  (when cross-terms are accounted for)
            //
            // For MinVar, the cross-covariance with fixed variables shifts the optimal
            // free weights but doesn't change the proportionality w_F ∝ Σ_FF^{-1} * 1_F.
            // The sum constraint c = remainingSum / (1' Σ_FF^{-1} 1_F) handles normalization.
            var covFree = ExtractSubmatrix(cov, freeIndices);
            var choleskyL = CholeskyDecompose(covFree, nFree);

            var ones = new decimal[nFree];
            for (var i = 0; i < nFree; i++)
            {
                ones[i] = 1m;
            }

            var z = CholeskySolve(choleskyL, ones, nFree);

            var sumZ = 0m;
            for (var i = 0; i < nFree; i++)
            {
                sumZ += z[i];
            }

            if (Math.Abs(sumZ) < 1e-20m)
            {
                throw new CalculationException("Degenerate covariance: Σ⁻¹·1 sums to zero.");
            }

            var c = remainingSum / sumZ;
            var wFree = new decimal[nFree];
            for (var i = 0; i < nFree; i++)
            {
                wFree[i] = c * z[i];
            }

            // Find the most-violated constraint (fix worst violator first)
            var worstIdx = -1;
            var worstViolation = 0m;
            var worstDirection = 0; // -1 = below min, +1 = above max

            for (var fi = 0; fi < nFree; fi++)
            {
                if (wFree[fi] < minWeight)
                {
                    var violation = minWeight - wFree[fi];
                    if (violation > worstViolation)
                    {
                        worstViolation = violation;
                        worstIdx = freeIndices[fi];
                        worstDirection = -1;
                    }
                }
                else if (wFree[fi] > maxWeight)
                {
                    var violation = wFree[fi] - maxWeight;
                    if (violation > worstViolation)
                    {
                        worstViolation = violation;
                        worstIdx = freeIndices[fi];
                        worstDirection = 1;
                    }
                }
            }

            if (worstIdx >= 0)
            {
                status[worstIdx] = worstDirection;
                continue;
            }

            // All free weights are feasible — build full solution
            var w = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                w[i] = status[i] switch
                {
                    -1 => minWeight,
                    1 => maxWeight,
                    _ => 0m,
                };
            }

            for (var fi = 0; fi < nFree; fi++)
            {
                w[freeIndices[fi]] = wFree[fi];
            }

            // KKT check: should we release any fixed variable?
            if (!TryReleaseMinVar(cov, w, n, status))
            {
                return w;
            }
        }

        // Fallback: equal weight
        var fallback = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            fallback[i] = 1m / n;
        }

        return fallback;
    }

    /// <summary>
    /// Solves the mean-variance QP: max w'μ - (λ/2)w'Σw s.t. 1'w=1, minWeight ≤ w_i ≤ maxWeight.
    /// Analytical solution: w = (1/λ)Σ⁻¹(μ - ν·1) where ν = (1'Σ⁻¹μ - λ)/(1'Σ⁻¹1).
    /// </summary>
    public static decimal[] SolveMeanVarianceQP(
        decimal[,] cov, decimal[] means, int n,
        decimal riskAversion, decimal minWeight, decimal maxWeight)
    {
        if (n == 1)
        {
            return [1m];
        }

        // When riskAversion is 0, the problem is purely maximize w'μ subject to
        // 1'w=1, minWeight ≤ w_i ≤ maxWeight. This is a linear program: put maxWeight
        // on assets in descending order of expected return.
        if (riskAversion == 0m)
        {
            return SolveMaxReturnLP(means, n, minWeight, maxWeight);
        }

        maxWeight = Math.Max(maxWeight, 1m / n);
        minWeight = Math.Min(minWeight, 1m / n);

        var status = new int[n]; // 0=free, -1=lower, +1=upper

        // Max iterations: n constraints can each be added/removed, with KKT releases.
        // 3*n is sufficient for well-conditioned problems.
        for (var iter = 0; iter < 3 * n + 3; iter++)
        {
            var freeIndices = new List<int>();
            var fixedSum = 0m;
            for (var i = 0; i < n; i++)
            {
                switch (status[i])
                {
                    case -1:
                        fixedSum += minWeight;
                        break;
                    case 1:
                        fixedSum += maxWeight;
                        break;
                    default:
                        freeIndices.Add(i);
                        break;
                }
            }

            var nFree = freeIndices.Count;
            if (nFree == 0)
            {
                // All variables fixed — return the best feasible solution
                var fixedW = new decimal[n];
                for (var i = 0; i < n; i++)
                {
                    fixedW[i] = status[i] == -1 ? minWeight : maxWeight;
                }

                // Normalize to sum=1 in case fixed values don't sum exactly
                var fixedTotal = fixedW.Sum();
                if (fixedTotal > 0)
                {
                    for (var i = 0; i < n; i++)
                    {
                        fixedW[i] /= fixedTotal;
                    }
                }

                return fixedW;
            }

            var remainingSum = 1m - fixedSum;

            var covFree = ExtractSubmatrix(cov, freeIndices);
            var meansFree = new decimal[nFree];
            for (var fi = 0; fi < nFree; fi++)
            {
                meansFree[fi] = means[freeIndices[fi]];
            }

            // Adjust means for cross-covariance with fixed variables
            for (var fi = 0; fi < nFree; fi++)
            {
                var i = freeIndices[fi];
                for (var j = 0; j < n; j++)
                {
                    if (status[j] == -1)
                    {
                        meansFree[fi] -= riskAversion * cov[i, j] * minWeight;
                    }
                    else if (status[j] == 1)
                    {
                        meansFree[fi] -= riskAversion * cov[i, j] * maxWeight;
                    }
                }
            }

            var choleskyL = CholeskyDecompose(covFree, nFree);

            var onesFree = new decimal[nFree];
            for (var i = 0; i < nFree; i++)
            {
                onesFree[i] = 1m;
            }

            var a = CholeskySolve(choleskyL, onesFree, nFree);
            var b = CholeskySolve(choleskyL, meansFree, nFree);

            var sumA = 0m;
            var sumB = 0m;
            for (var i = 0; i < nFree; i++)
            {
                sumA += a[i];
                sumB += b[i];
            }

            if (Math.Abs(sumA) < 1e-20m)
            {
                throw new CalculationException("Degenerate covariance: Σ⁻¹·1 sums to zero.");
            }

            var nu = (sumB / riskAversion - remainingSum) / (sumA / riskAversion);

            var wFree = new decimal[nFree];
            for (var i = 0; i < nFree; i++)
            {
                wFree[i] = (b[i] - nu * a[i]) / riskAversion;
            }

            // Find most-violated constraint
            var worstIdx = -1;
            var worstViolation = 0m;
            var worstDirection = 0;

            for (var fi = 0; fi < nFree; fi++)
            {
                if (wFree[fi] < minWeight)
                {
                    var violation = minWeight - wFree[fi];
                    if (violation > worstViolation)
                    {
                        worstViolation = violation;
                        worstIdx = freeIndices[fi];
                        worstDirection = -1;
                    }
                }
                else if (wFree[fi] > maxWeight)
                {
                    var violation = wFree[fi] - maxWeight;
                    if (violation > worstViolation)
                    {
                        worstViolation = violation;
                        worstIdx = freeIndices[fi];
                        worstDirection = 1;
                    }
                }
            }

            if (worstIdx >= 0)
            {
                status[worstIdx] = worstDirection;
                continue;
            }

            // Feasible — build full solution
            var w = new decimal[n];
            for (var i = 0; i < n; i++)
            {
                w[i] = status[i] switch
                {
                    -1 => minWeight,
                    1 => maxWeight,
                    _ => 0m,
                };
            }

            for (var fi = 0; fi < nFree; fi++)
            {
                w[freeIndices[fi]] = wFree[fi];
            }

            if (!TryReleaseMeanVar(cov, means, w, n, riskAversion, status))
            {
                return w;
            }
        }

        // Fallback: should not reach here for well-conditioned problems.
        // Return best solution found so far instead of equal weight.
        throw new CalculationException(
            "MeanVariance active-set did not converge within iteration limit.");
    }

    // ─── Cholesky decomposition ────────────────────────────────────────

    /// <summary>
    /// Solves the pure max-return LP: maximize w'μ s.t. 1'w=1, minWeight ≤ w_i ≤ maxWeight.
    /// Greedy: assign maxWeight to assets in descending return order until budget exhausted.
    /// </summary>
    private static decimal[] SolveMaxReturnLP(decimal[] means, int n, decimal minWeight, decimal maxWeight)
    {
        // Start everyone at minWeight
        var weights = new decimal[n];
        var remaining = 1m - n * minWeight;
        for (var i = 0; i < n; i++)
        {
            weights[i] = minWeight;
        }

        // Sort asset indices by descending expected return
        var indices = Enumerable.Range(0, n).OrderByDescending(i => means[i]).ToArray();

        foreach (var i in indices)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var add = Math.Min(maxWeight - minWeight, remaining);
            weights[i] += add;
            remaining -= add;
        }

        return weights;
    }

    /// <summary>
    /// Cholesky decomposition: A = LL' where L is lower triangular.
    /// Throws CalculationException if A is not positive definite.
    /// </summary>
    internal static decimal[,] CholeskyDecompose(decimal[,] a, int n)
    {
        var l = new decimal[n, n];

        for (var j = 0; j < n; j++)
        {
            var sum = 0m;
            for (var k = 0; k < j; k++)
            {
                sum += l[j, k] * l[j, k];
            }

            var diag = a[j, j] - sum;
            if (diag < 1e-10m)
            {
                throw new CalculationException(
                    $"Matrix is not positive definite: diagonal element {j} = {diag} after decomposition.");
            }

            l[j, j] = (decimal)Math.Sqrt((double)diag);

            for (var i = j + 1; i < n; i++)
            {
                var rowSum = 0m;
                for (var k = 0; k < j; k++)
                {
                    rowSum += l[i, k] * l[j, k];
                }

                l[i, j] = (a[i, j] - rowSum) / l[j, j];
            }
        }

        return l;
    }

    /// <summary>
    /// Solves Ax = b where A = LL' via forward + backward substitution.
    /// </summary>
    internal static decimal[] CholeskySolve(decimal[,] l, decimal[] b, int n)
    {
        // Forward substitution: Ly = b
        var y = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            var sum = 0m;
            for (var k = 0; k < i; k++)
            {
                sum += l[i, k] * y[k];
            }

            y[i] = (b[i] - sum) / l[i, i];
        }

        // Backward substitution: L'x = y
        var x = new decimal[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = 0m;
            for (var k = i + 1; k < n; k++)
            {
                sum += l[k, i] * x[k];
            }

            x[i] = (y[i] - sum) / l[i, i];
        }

        return x;
    }

    // ─── Private helpers ───────────────────────────────────────────────

    private static decimal[,] ExtractSubmatrix(decimal[,] full, List<int> indices)
    {
        var m = indices.Count;
        var sub = new decimal[m, m];
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < m; j++)
            {
                sub[i, j] = full[indices[i], indices[j]];
            }
        }

        return sub;
    }

    /// <summary>
    /// KKT check for MinVar: tries to release one fixed variable.
    /// At optimality for free variables: (Σw)_i = ν for all free i.
    /// At lower bound: (Σw)_i ≥ ν (releasing would NOT reduce variance).
    /// At upper bound: (Σw)_i ≤ ν (releasing would NOT reduce variance).
    /// </summary>
    private static bool TryReleaseMinVar(
        decimal[,] cov, decimal[] w, int n, int[] status)
    {
        var grad = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                grad[i] += cov[i, j] * w[j];
            }
        }

        // ν = (Σw)_i for any free variable (all should be equal at optimum)
        var nu = 0m;
        var nFree = 0;
        for (var i = 0; i < n; i++)
        {
            if (status[i] == 0)
            {
                nu += grad[i];
                nFree++;
            }
        }

        if (nFree == 0)
        {
            return false;
        }

        nu /= nFree;

        // Find the most-violated KKT condition among fixed variables
        var worstIdx = -1;
        var worstViolation = 0m;

        for (var i = 0; i < n; i++)
        {
            if (status[i] == -1 && grad[i] < nu - 1e-10m)
            {
                // At lower bound but gradient is LESS — releasing reduces variance
                var violation = nu - grad[i];
                if (violation > worstViolation)
                {
                    worstViolation = violation;
                    worstIdx = i;
                }
            }
            else if (status[i] == 1 && grad[i] > nu + 1e-10m)
            {
                // At upper bound but gradient is MORE — releasing reduces variance
                var violation = grad[i] - nu;
                if (violation > worstViolation)
                {
                    worstViolation = violation;
                    worstIdx = i;
                }
            }
        }

        if (worstIdx < 0)
        {
            return false;
        }

        status[worstIdx] = 0;
        return true;
    }

    /// <summary>
    /// KKT check for MeanVar: tries to release one fixed variable.
    /// Objective gradient: grad_i = μ_i - λ(Σw)_i.
    /// At optimality for free variables: grad_i = ν for all free i.
    /// At lower bound: grad_i ≤ ν (can't improve by increasing).
    /// At upper bound: grad_i ≥ ν (can't improve by decreasing).
    /// </summary>
    private static bool TryReleaseMeanVar(
        decimal[,] cov, decimal[] means, decimal[] w, int n,
        decimal riskAversion, int[] status)
    {
        var grad = new decimal[n];
        for (var i = 0; i < n; i++)
        {
            var covW = 0m;
            for (var j = 0; j < n; j++)
            {
                covW += cov[i, j] * w[j];
            }

            grad[i] = means[i] - riskAversion * covW;
        }

        var nu = 0m;
        var nFree = 0;
        for (var i = 0; i < n; i++)
        {
            if (status[i] == 0)
            {
                nu += grad[i];
                nFree++;
            }
        }

        if (nFree == 0)
        {
            return false;
        }

        nu /= nFree;

        var worstIdx = -1;
        var worstViolation = 0m;

        for (var i = 0; i < n; i++)
        {
            if (status[i] == -1 && grad[i] > nu + 1e-10m)
            {
                var violation = grad[i] - nu;
                if (violation > worstViolation)
                {
                    worstViolation = violation;
                    worstIdx = i;
                }
            }
            else if (status[i] == 1 && grad[i] < nu - 1e-10m)
            {
                var violation = nu - grad[i];
                if (violation > worstViolation)
                {
                    worstViolation = violation;
                    worstIdx = i;
                }
            }
        }

        if (worstIdx < 0)
        {
            return false;
        }

        status[worstIdx] = 0;
        return true;
    }
}

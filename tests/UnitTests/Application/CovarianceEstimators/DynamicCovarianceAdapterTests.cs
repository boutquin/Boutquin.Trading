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

using Boutquin.Trading.Application.Configuration;
using Boutquin.Trading.Application.CovarianceEstimators;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boutquin.Trading.Tests.UnitTests.Application.CovarianceEstimators;

/// <summary>
/// Unit tests for <see cref="DynamicCovarianceAdapter"/> — the bridge between
/// <see cref="IDynamicCovarianceEstimator"/> and <see cref="ICovarianceEstimator"/>.
/// </summary>
public sealed class DynamicCovarianceAdapterTests
{
    private const double PsdEigenvalueTolerance = -1e-10;

    // ── Synthetic data helper ──────────────────────────────────────────────────

    /// <summary>
    /// Generates a deterministic N-asset, T-observation synthetic return panel.
    /// </summary>
    private static decimal[][] GenerateSyntheticReturns(int assets, int observations, int seed = 42)
    {
        var rng = new Random(seed);
        var panel = new decimal[assets][];
        var market = new double[observations];
        for (var k = 0; k < observations; k++)
        {
            market[k] = 0.008 * (rng.NextDouble() - 0.5);
        }

        for (var i = 0; i < assets; i++)
        {
            var series = new double[observations];
            var vol = 0.01;
            for (var k = 0; k < observations; k++)
            {
                vol = 0.0008 + 0.85 * vol + 0.1 * Math.Abs(series[Math.Max(0, k - 1)]);
                var idio = vol * (rng.NextDouble() - 0.5);
                series[k] = 0.6 * market[k] + idio;
            }

            panel[i] = series.Select(x => (decimal)x).ToArray();
        }

        return panel;
    }

    // ── Eigenvalue helper for PSD check ───────────────────────────────────────

    private static double SmallestEigenvalue(decimal[,] m)
    {
        var n = m.GetLength(0);
        var a = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                a[i, j] = (double)m[i, j];
            }
        }

        // Gershgorin upper bound for the spectral radius.
        var shift = 0.0;
        for (var i = 0; i < n; i++)
        {
            var row = 0.0;
            for (var j = 0; j < n; j++)
            {
                row += Math.Abs(a[i, j]);
            }

            shift = Math.Max(shift, row);
        }

        // B = shift*I - A; largest eigenvalue of B = shift - lambda_min(A).
        var b = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                b[i, j] = (i == j ? shift : 0.0) - a[i, j];
            }
        }

        var v = Enumerable.Range(0, n).Select(_ => 1.0).ToArray();
        var lambda = 0.0;
        for (var iter = 0; iter < 1000; iter++)
        {
            var w = new double[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    w[i] += b[i, j] * v[j];
                }
            }

            var norm = Math.Sqrt(w.Sum(x => x * x));
            if (norm < 1e-300)
            {
                break;
            }

            for (var i = 0; i < n; i++)
            {
                v[i] = w[i] / norm;
            }

            lambda = v.Zip(w, (vi, wi) => vi * wi).Sum();
        }

        return shift - lambda; // lambda_min(A) = shift - lambda_max(B)
    }

    // ── Test 1: Fidelity — result equals inner.Forecast() after Fit ───────────

    /// <summary>
    /// Verifies that <see cref="DynamicCovarianceAdapter.Estimate"/> returns the same
    /// matrix as <see cref="DccNlCovarianceEstimator.Forecast"/> after an independent
    /// <see cref="DccNlCovarianceEstimator.Fit"/> on the same return panel.
    /// </summary>
    [Fact]
    public void Estimate_ReturnsForecastOfInnerEstimator()
    {
        var inner = new DccNlCovarianceEstimator();
        var adapter = new DynamicCovarianceAdapter(inner);
        var returns = GenerateSyntheticReturns(assets: 3, observations: 30);

        // Adapter drives Fit + Forecast internally.
        var result = adapter.Estimate(returns);

        // Independent reference: Fit the same inner instance again, then read Forecast.
        inner.Fit(returns);
        var expected = inner.Forecast();

        // All elements must match exactly (same deterministic path).
        result.Should().BeEquivalentTo(expected);
    }

    // ── Test 2: PSD guarantee ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the matrix returned by <see cref="DynamicCovarianceAdapter.Estimate"/>
    /// is positive semi-definite — all eigenvalues ≥ <c>-1e-10</c>.
    /// </summary>
    [Fact]
    public void Estimate_ReturnsPositiveSemiDefiniteMatrix()
    {
        var adapter = new DynamicCovarianceAdapter(new DccNlCovarianceEstimator());
        var returns = GenerateSyntheticReturns(assets: 3, observations: 30);

        var result = adapter.Estimate(returns);

        var minEigenvalue = SmallestEigenvalue(result);
        minEigenvalue.Should().BeGreaterThanOrEqualTo(PsdEigenvalueTolerance,
            because: "the one-step-ahead DCC-NL forecast must be positive semi-definite");
    }

    // ── Test 3: Null guard ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="DynamicCovarianceAdapter"/> throws
    /// <see cref="ArgumentNullException"/> when <c>inner</c> is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new DynamicCovarianceAdapter(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("inner");
    }

    // ── Test 4: DI error message includes "DccNl" ─────────────────────────────

    /// <summary>
    /// Verifies that an unknown covariance estimator key causes
    /// <see cref="ArgumentOutOfRangeException"/> whose message includes <c>"DccNl"</c>
    /// (confirming the valid-values list in <c>ServiceCollectionExtensions</c> was updated).
    /// Driven through the DI surface because <c>CreateCovarianceEstimator</c> is private.
    /// </summary>
    [Fact]
    public void CreateCovarianceEstimator_UnknownKey_ErrorMessageIncludesDccNl()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Backtest:StartDate"] = "2020-01-01",
            ["Backtest:EndDate"] = "2025-12-31",
            ["Backtest:BaseCurrency"] = "USD",
            ["Backtest:RebalancingFrequency"] = "Monthly",
            ["Backtest:ConstructionModel"] = "MinimumVariance",
            ["Backtest:CovarianceEstimator"] = "unknown_estimator",
            ["CostModel:TransactionCostType"] = "FixedPerTrade",
            ["CostModel:CommissionRate"] = "10",
            ["CostModel:SlippageType"] = "NoSlippage",
            ["CostModel:SlippageAmount"] = "0",
            ["CostModel:VolumeLimit"] = "0",
            ["RiskManagement:MaxDrawdownPercent"] = "0.20",
            ["RiskManagement:MaxPositionSizePercent"] = "0.25",
            ["RiskManagement:MaxSectorExposurePercent"] = "0",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddBoutquinTrading(configuration);
        var sp = services.BuildServiceProvider();

        var act = sp.GetRequiredService<IPortfolioConstructionModel>;
        act.Should()
           .Throw<ArgumentOutOfRangeException>()
           .WithMessage("*DccNl*");
    }
}

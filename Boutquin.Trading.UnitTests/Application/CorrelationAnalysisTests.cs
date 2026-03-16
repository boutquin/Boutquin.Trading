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

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.Analytics;
using FluentAssertions;

/// <summary>
/// Tests for correlation analysis — rolling correlation matrix and diversification ratio.
/// </summary>
public sealed class CorrelationAnalysisTests
{
    private const decimal Precision = 1e-8m;

    // --- RP3-03 Test: Perfectly correlated assets → diversification ratio = 1.0 ---

    [Fact]
    public void Analyze_PerfectlyCorrelatedAssets_DiversificationRatioShouldBeOne()
    {
        // B = 2*A, perfect correlation
        var assetNames = new[] { "A", "B" };
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.02m, -0.04m, 0.06m, -0.02m, 0.04m }
        };
        var weights = new[] { 0.5m, 0.5m };

        var result = CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        result.DiversificationRatio.Should().BeApproximately(1.0m, 0.01m,
            "Perfectly correlated assets have diversification ratio = 1.0");

        // Correlation between A and B should be 1.0
        result.CorrelationMatrix[0, 1].Should().BeApproximately(1.0m, Precision);
        result.CorrelationMatrix[1, 0].Should().BeApproximately(1.0m, Precision);
    }

    // --- RP3-03 Test: Uncorrelated assets → diversification ratio > 1 ---

    [Fact]
    public void Analyze_UncorrelatedAssets_DiversificationRatioShouldBeGreaterThanOne()
    {
        var assetNames = new[] { "A", "B" };
        var returns = new[]
        {
            new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m, 0.01m, -0.01m },
            new[] { 0.01m, 0.01m, -0.01m, -0.01m, 0.01m, 0.01m, -0.01m, -0.01m }
        };
        var weights = new[] { 0.5m, 0.5m };

        var result = CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        result.DiversificationRatio.Should().BeGreaterThan(1.0m,
            "Uncorrelated assets should have diversification ratio > 1.0");
    }

    // --- RP3-03 Test: Diagonal of correlation matrix should be 1.0 ---

    [Fact]
    public void Analyze_DiagonalShouldBeOne()
    {
        var assetNames = new[] { "A", "B", "C" };
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.005m, 0.01m, -0.005m, 0.015m, -0.01m },
            new[] { -0.01m, 0.02m, -0.015m, 0.005m, 0.01m }
        };
        var weights = new[] { 0.4m, 0.3m, 0.3m };

        var result = CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        for (var i = 0; i < 3; i++)
        {
            result.CorrelationMatrix[i, i].Should().BeApproximately(1.0m, Precision,
                $"Diagonal element [{i},{i}] should be 1.0");
        }
    }

    // --- RP3-03 Test: Correlation matrix should be symmetric ---

    [Fact]
    public void Analyze_CorrelationMatrixShouldBeSymmetric()
    {
        var assetNames = new[] { "A", "B", "C" };
        var returns = new[]
        {
            new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m },
            new[] { 0.005m, 0.01m, -0.005m, 0.015m, -0.01m },
            new[] { -0.01m, 0.02m, -0.015m, 0.005m, 0.01m }
        };
        var weights = new[] { 0.4m, 0.3m, 0.3m };

        var result = CorrelationAnalyzer.Analyze(assetNames, returns, weights);

        for (var i = 0; i < 3; i++)
        {
            for (var j = i + 1; j < 3; j++)
            {
                result.CorrelationMatrix[i, j].Should().BeApproximately(
                    result.CorrelationMatrix[j, i], Precision,
                    $"Matrix should be symmetric at [{i},{j}]");
            }
        }
    }

    // --- RP3-03 Test: Rolling correlation produces time series ---

    [Fact]
    public void RollingCorrelation_ShouldProduceTimeSeries()
    {
        // 20 observations, window of 5
        var returnsA = new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, 0.005m, -0.015m, 0.025m, -0.005m, 0.01m,
                               0.015m, -0.01m, 0.02m, -0.02m, 0.01m, 0.005m, -0.01m, 0.03m, -0.015m, 0.02m };
        var returnsB = new[] { 0.005m, 0.01m, -0.005m, 0.015m, -0.01m, 0.02m, -0.01m, 0.005m, 0.01m, -0.005m,
                               0.008m, -0.012m, 0.015m, -0.008m, 0.005m, 0.01m, -0.005m, 0.02m, -0.01m, 0.015m };

        var rollingCorrelations = CorrelationAnalyzer.RollingCorrelation(returnsA, returnsB, windowSize: 5);

        // With 20 observations and window 5, should produce 16 values (20 - 5 + 1)
        rollingCorrelations.Should().HaveCount(16);

        // All correlations should be between -1 and 1
        foreach (var corr in rollingCorrelations)
        {
            corr.Should().BeGreaterThanOrEqualTo(-1.0m);
            corr.Should().BeLessThanOrEqualTo(1.0m);
        }
    }
}

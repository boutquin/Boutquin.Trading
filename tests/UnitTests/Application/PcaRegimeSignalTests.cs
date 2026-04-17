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
/// Tests for <see cref="PcaRegimeSignal"/>.
/// </summary>
public sealed class PcaRegimeSignalTests
{
    /// <summary>
    /// With an identity correlation matrix (uncorrelated assets), PC1 variance share
    /// should equal 1/N — no dominant factor.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void ComputeVarianceShare_IdentityCorrelation_ReturnsOneOverN(int n)
    {
        var identity = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            identity[i, i] = 1m;
        }

        var result = PcaRegimeSignal.ComputeVarianceShare(identity);

        result.Pc1VarianceShare.Should().BeApproximately(1m / n, 0.01m,
            $"PC1 variance share of {n}x{n} identity should be ~1/{n}");
    }

    /// <summary>
    /// With perfect correlation, PC1 should capture all variance (share ≈ 1).
    /// </summary>
    [Fact]
    public void ComputeVarianceShare_PerfectCorrelation_ReturnsOne()
    {
        const int n = 3;
        var ones = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                ones[i, j] = 1m;
            }
        }

        var result = PcaRegimeSignal.ComputeVarianceShare(ones);

        result.Pc1VarianceShare.Should().BeApproximately(1m, 0.05m,
            "Perfect correlation should yield PC1 variance share ≈ 1");
    }

    /// <summary>
    /// Variance shares should sum to 1 and be in descending order.
    /// </summary>
    [Fact]
    public void ComputeVarianceShare_ThreeAssets_SharesSumToOneDescending()
    {
        var corr = new decimal[3, 3];
        corr[0, 0] = 1m; corr[0, 1] = 0.5m; corr[0, 2] = 0.3m;
        corr[1, 0] = 0.5m; corr[1, 1] = 1m; corr[1, 2] = 0.2m;
        corr[2, 0] = 0.3m; corr[2, 1] = 0.2m; corr[2, 2] = 1m;

        var result = PcaRegimeSignal.ComputeVarianceShare(corr);

        result.AllVarianceShares.Sum().Should().BeApproximately(1m, 0.01m,
            "All variance shares should sum to 1");

        for (var i = 0; i < result.AllVarianceShares.Length - 1; i++)
        {
            result.AllVarianceShares[i].Should().BeGreaterThanOrEqualTo(result.AllVarianceShares[i + 1],
                "Variance shares should be in descending order");
        }
    }

    /// <summary>
    /// ComputeVarianceShareFromReturns should work with return series (not just correlation matrix).
    /// </summary>
    [Fact]
    public void ComputeVarianceShareFromReturns_HighlyCorrelated_HighPc1Share()
    {
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var result = PcaRegimeSignal.ComputeVarianceShareFromReturns(returns);

        // Two of three assets are highly correlated → PC1 should be dominant
        result.Pc1VarianceShare.Should().BeGreaterThan(0.5m,
            "Correlated assets should produce a dominant PC1");
    }

    /// <summary>
    /// Eigenvector stability between two identical correlation matrices should be 1.0 (perfect).
    /// </summary>
    [Fact]
    public void ComputeEigenvectorStability_IdenticalMatrices_ReturnsOne()
    {
        var corr = new decimal[3, 3];
        corr[0, 0] = 1m; corr[0, 1] = 0.5m; corr[0, 2] = 0.3m;
        corr[1, 0] = 0.5m; corr[1, 1] = 1m; corr[1, 2] = 0.2m;
        corr[2, 0] = 0.3m; corr[2, 1] = 0.2m; corr[2, 2] = 1m;

        var stability = PcaRegimeSignal.ComputeEigenvectorStability(corr, corr);

        stability.Pc1CosineSimilarity.Should().BeApproximately(1m, 0.01m,
            "Identical matrices should have perfect eigenvector stability");
    }

    /// <summary>
    /// Eigenvector stability between very different matrices should be low.
    /// </summary>
    [Fact]
    public void ComputeEigenvectorStability_DifferentMatrices_ReturnsLow()
    {
        // Matrix A: assets 0-1 correlated
        var corrA = new decimal[3, 3];
        corrA[0, 0] = 1m; corrA[0, 1] = 0.9m; corrA[0, 2] = 0.1m;
        corrA[1, 0] = 0.9m; corrA[1, 1] = 1m; corrA[1, 2] = 0.1m;
        corrA[2, 0] = 0.1m; corrA[2, 1] = 0.1m; corrA[2, 2] = 1m;

        // Matrix B: assets 0-2 correlated instead
        var corrB = new decimal[3, 3];
        corrB[0, 0] = 1m; corrB[0, 1] = 0.1m; corrB[0, 2] = 0.9m;
        corrB[1, 0] = 0.1m; corrB[1, 1] = 1m; corrB[1, 2] = 0.1m;
        corrB[2, 0] = 0.9m; corrB[2, 1] = 0.1m; corrB[2, 2] = 1m;

        var stability = PcaRegimeSignal.ComputeEigenvectorStability(corrA, corrB);

        stability.Pc1CosineSimilarity.Should().BeLessThan(0.95m,
            "Different correlation structures should produce different eigenvectors");
    }

    /// <summary>
    /// Single-asset correlation matrix should return PC1 share of 1.
    /// </summary>
    [Fact]
    public void ComputeVarianceShare_SingleAsset_ReturnsOne()
    {
        var single = new decimal[1, 1];
        single[0, 0] = 1m;

        var result = PcaRegimeSignal.ComputeVarianceShare(single);

        result.Pc1VarianceShare.Should().Be(1m);
    }

    [Fact]
    public void ComputeVarianceShare_NullMatrix_ThrowsArgumentException()
    {
        var act = () => PcaRegimeSignal.ComputeVarianceShare(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

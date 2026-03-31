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
/// Tests for <see cref="EffectiveNumberOfBets"/>.
/// </summary>
public sealed class EffectiveNumberOfBetsTests
{
    /// <summary>
    /// An N x N identity correlation matrix has equal eigenvalues, so ENB should equal N.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Compute_IdentityMatrix_ReturnsN(int n)
    {
        var identity = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            identity[i, i] = 1m;
        }

        var enb = EffectiveNumberOfBets.Compute(identity);

        enb.Should().BeApproximately(n, 0.01m,
            $"ENB of {n}x{n} identity matrix should be approximately {n}");
    }

    /// <summary>
    /// When all correlations are 1 (perfect correlation), ENB should be approximately 1.
    /// </summary>
    [Fact]
    public void Compute_PerfectCorrelation_ReturnsOne()
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

        var enb = EffectiveNumberOfBets.Compute(ones);

        enb.Should().BeApproximately(1m, 0.05m,
            "Perfect correlation should yield ENB approximately 1");
    }

    /// <summary>
    /// 2 x 2 identity (zero correlation) should give ENB = 2.
    /// </summary>
    [Fact]
    public void Compute_TwoByTwoZeroCorrelation_ReturnsTwo()
    {
        var identity = new decimal[2, 2];
        identity[0, 0] = 1m;
        identity[1, 1] = 1m;

        var enb = EffectiveNumberOfBets.Compute(identity);

        enb.Should().BeApproximately(2m, 0.01m,
            "2x2 identity should give ENB = 2");
    }

    /// <summary>
    /// A single asset trivially has ENB = 1.
    /// </summary>
    [Fact]
    public void Compute_SingleAsset_ReturnsOne()
    {
        var single = new decimal[1, 1];
        single[0, 0] = 1m;

        var enb = EffectiveNumberOfBets.Compute(single);

        enb.Should().Be(1m, "Single asset should have ENB = 1");
    }

    /// <summary>
    /// Null matrix input should throw ArgumentException.
    /// </summary>
    [Fact]
    public void Compute_NullMatrix_ThrowsArgumentException()
    {
        var act = () => EffectiveNumberOfBets.Compute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Uncorrelated return series should produce a high ENB (close to N).
    /// Uses 2 assets to avoid decimal overflow in the Jacobi eigendecomposition.
    /// </summary>
    [Fact]
    public void ComputeFromReturns_UncorrelatedReturns_HighENB()
    {
        // Construct 2 uncorrelated return series.
        var returns = new[]
        {
            new[] { 1m, -1m, 2m, -2m, 1m, -1m, 2m, -2m, 1m, -1m },
            new[] { 1m, 1m, -1m, -1m, 2m, -2m, 1m, 1m, -1m, -1m }
        };

        var enb = EffectiveNumberOfBets.ComputeFromReturns(returns);

        // With 2 approximately uncorrelated assets, ENB should be close to 2
        enb.Should().BeGreaterThan(1.5m,
            "Uncorrelated assets should produce ENB well above 1");
        enb.Should().BeLessThanOrEqualTo(2m,
            "ENB cannot exceed the number of assets");
    }
}

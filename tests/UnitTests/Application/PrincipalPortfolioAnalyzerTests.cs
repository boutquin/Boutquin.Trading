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
/// Tests for <see cref="PrincipalPortfolioAnalyzer"/>.
/// </summary>
public sealed class PrincipalPortfolioAnalyzerTests
{
    /// <summary>
    /// With an identity correlation matrix, principal portfolios should be the identity
    /// (each PC corresponds to exactly one asset).
    /// </summary>
    [Fact]
    public void Analyze_IdentityCorrelation_PrincipalPortfoliosAreAssets()
    {
        const int n = 3;
        var identity = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            identity[i, i] = 1m;
        }

        var variances = new[] { 0.04m, 0.01m, 0.09m }; // different volatilities

        var result = PrincipalPortfolioAnalyzer.Analyze(identity, variances);

        result.Portfolios.Should().HaveCount(n);

        // Each principal portfolio should have one dominant weight
        foreach (var pp in result.Portfolios)
        {
            pp.Weights.Max(Math.Abs).Should().BeGreaterThan(0.9m,
                "Each PC portfolio should be concentrated in one asset for identity correlation");
        }
    }

    /// <summary>
    /// Principal portfolio weights for each PC should sum to approximately 1
    /// (normalized to be investable).
    /// </summary>
    [Fact]
    public void Analyze_ThreeAssets_WeightsSumToOne()
    {
        var corr = new decimal[3, 3];
        corr[0, 0] = 1m; corr[0, 1] = 0.5m; corr[0, 2] = 0.3m;
        corr[1, 0] = 0.5m; corr[1, 1] = 1m; corr[1, 2] = 0.2m;
        corr[2, 0] = 0.3m; corr[2, 1] = 0.2m; corr[2, 2] = 1m;

        var variances = new[] { 0.04m, 0.02m, 0.06m };

        var result = PrincipalPortfolioAnalyzer.Analyze(corr, variances);

        foreach (var pp in result.Portfolios)
        {
            pp.Weights.Sum().Should().BeApproximately(1m, 0.01m,
                $"PC{pp.ComponentIndex} portfolio weights should sum to 1");
        }
    }

    /// <summary>
    /// Risk contributions should sum to 1 (normalized) and be non-negative.
    /// </summary>
    [Fact]
    public void Analyze_ThreeAssets_RiskContributionsSumToOne()
    {
        var corr = new decimal[3, 3];
        corr[0, 0] = 1m; corr[0, 1] = 0.5m; corr[0, 2] = 0.3m;
        corr[1, 0] = 0.5m; corr[1, 1] = 1m; corr[1, 2] = 0.2m;
        corr[2, 0] = 0.3m; corr[2, 1] = 0.2m; corr[2, 2] = 1m;

        var variances = new[] { 0.04m, 0.02m, 0.06m };

        var result = PrincipalPortfolioAnalyzer.Analyze(corr, variances);

        result.Portfolios.Sum(p => p.RiskContribution).Should().BeApproximately(1m, 0.01m,
            "Risk contributions across all PCs should sum to 1");

        foreach (var pp in result.Portfolios)
        {
            pp.RiskContribution.Should().BeGreaterThanOrEqualTo(0m,
                "Risk contributions should be non-negative");
        }
    }

    /// <summary>
    /// The first principal portfolio should have the highest risk contribution.
    /// </summary>
    [Fact]
    public void Analyze_CorrelatedAssets_Pc1HasHighestRiskContribution()
    {
        var corr = new decimal[3, 3];
        corr[0, 0] = 1m; corr[0, 1] = 0.7m; corr[0, 2] = 0.5m;
        corr[1, 0] = 0.7m; corr[1, 1] = 1m; corr[1, 2] = 0.4m;
        corr[2, 0] = 0.5m; corr[2, 1] = 0.4m; corr[2, 2] = 1m;

        var variances = new[] { 0.04m, 0.03m, 0.05m };

        var result = PrincipalPortfolioAnalyzer.Analyze(corr, variances);

        result.Portfolios[0].RiskContribution.Should().BeGreaterThan(result.Portfolios[1].RiskContribution,
            "PC1 should have the highest risk contribution");
    }

    /// <summary>
    /// AnalyzeFromReturns should produce consistent results with the correlation matrix path.
    /// </summary>
    [Fact]
    public void AnalyzeFromReturns_ProducesValidResult()
    {
        var returns = new[]
        {
            new[] { 2m, -1m, 3m, -2m, 1m, 4m, -3m, 2m, -1m, 3m },
            new[] { 1.9m, -1.1m, 3.1m, -1.9m, 0.9m, 4.1m, -2.9m, 2.1m, -0.9m, 2.9m },
            new[] { 1m, 2m, -1m, -2m, 3m, -3m, 1m, -1m, 2m, -2m }
        };

        var result = PrincipalPortfolioAnalyzer.AnalyzeFromReturns(returns);

        result.Portfolios.Should().HaveCount(3);
        result.Portfolios.Sum(p => p.RiskContribution).Should().BeApproximately(1m, 0.01m);

        // Weights should sum to 1 for each principal portfolio
        foreach (var pp in result.Portfolios)
        {
            pp.Weights.Sum().Should().BeApproximately(1m, 0.05m,
                $"PC{pp.ComponentIndex} weights should sum to ~1");
        }
    }

    /// <summary>
    /// Single asset should produce one principal portfolio with weight 1.
    /// </summary>
    [Fact]
    public void Analyze_SingleAsset_ReturnsSinglePortfolio()
    {
        var single = new decimal[1, 1];
        single[0, 0] = 1m;

        var result = PrincipalPortfolioAnalyzer.Analyze(single, [0.04m]);

        result.Portfolios.Should().HaveCount(1);
        result.Portfolios[0].Weights[0].Should().BeApproximately(1m, 0.01m);
        result.Portfolios[0].RiskContribution.Should().BeApproximately(1m, 0.01m);
    }

    [Fact]
    public void Analyze_NullCorrelationMatrix_ThrowsArgumentException()
    {
        var act = () => PrincipalPortfolioAnalyzer.Analyze(null!, [0.04m]);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Analyze_MismatchedDimensions_ThrowsArgumentException()
    {
        var corr = new decimal[3, 3];
        for (var i = 0; i < 3; i++)
        {
            corr[i, i] = 1m;
        }

        var act = () => PrincipalPortfolioAnalyzer.Analyze(corr, [0.04m, 0.02m]); // 2 variances, 3x3 matrix

        act.Should().Throw<ArgumentException>();
    }
}

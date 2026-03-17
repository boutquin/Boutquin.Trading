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
/// Tests for multi-factor regression of portfolio returns against risk factors.
/// </summary>
public sealed class FactorRegressionTests
{
    private const decimal Precision = 1e-6m;

    // --- RP3-02 Test: Pure market portfolio has Beta≈1, all other loadings≈0 ---

    [Fact]
    public void Regress_PureMarketPortfolio_ShouldHaveBetaApprox1()
    {
        // Portfolio returns = market returns (perfect correlation, beta=1, alpha=0)
        var portfolioReturns = new[] { 0.01m, -0.02m, 0.015m, 0.005m, -0.01m, 0.02m, -0.005m, 0.01m, 0.003m, -0.008m };
        var factorNames = new[] { "Mkt-Rf" };
        var factorReturns = new[]
        {
            new[] { 0.01m, -0.02m, 0.015m, 0.005m, -0.01m, 0.02m, -0.005m, 0.01m, 0.003m, -0.008m }
        };

        var result = FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        result.FactorLoadings["Mkt-Rf"].Should().BeApproximately(1.0m, 0.01m,
            "Pure market portfolio should have beta ≈ 1");
        result.Alpha.Should().BeApproximately(0m, Precision,
            "Pure market portfolio should have alpha ≈ 0");
        result.RSquared.Should().BeApproximately(1.0m, 0.01m,
            "Perfect fit should have R² ≈ 1");
    }

    // --- RP3-02 Test: R² > 0 for diversified portfolio ---

    [Fact]
    public void Regress_DiversifiedPortfolio_ShouldHavePositiveRSquared()
    {
        // Portfolio is correlated with market but not perfectly
        var portfolioReturns = new[] { 0.012m, -0.015m, 0.018m, 0.003m, -0.008m, 0.022m, -0.003m, 0.011m, 0.005m, -0.006m };
        var factorNames = new[] { "Mkt-Rf", "SMB", "HML" };
        var factorReturns = new[]
        {
            new[] { 0.01m, -0.02m, 0.015m, 0.005m, -0.01m, 0.02m, -0.005m, 0.01m, 0.003m, -0.008m }, // Market
            new[] { 0.002m, 0.001m, -0.001m, 0.003m, -0.002m, 0.001m, 0.002m, -0.001m, 0.001m, -0.003m }, // Size
            new[] { 0.001m, 0.003m, -0.002m, -0.001m, 0.002m, 0.001m, -0.001m, 0.002m, -0.001m, 0.001m } // Value
        };

        var result = FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        result.RSquared.Should().BeGreaterThan(0m, "Diversified portfolio should have R² > 0");
        result.RSquared.Should().BeLessThanOrEqualTo(1.0m, "R² must be at most 1.0");
        result.FactorLoadings.Should().HaveCount(3);
    }

    // --- RP3-02 Test: Value-tilted portfolio has positive HML loading ---

    [Fact]
    public void Regress_ValueTiltedPortfolio_ShouldHavePositiveHMLLoading()
    {
        // Portfolio = 0.8*Market + 0.5*HML + noise
        var mktReturns = new[] { 0.01m, -0.02m, 0.015m, 0.005m, -0.01m, 0.02m, -0.005m, 0.01m, 0.003m, -0.008m };
        var hmlReturns = new[] { 0.003m, 0.005m, -0.002m, 0.004m, -0.001m, 0.003m, 0.001m, 0.002m, -0.001m, 0.004m };

        var portfolioReturns = new decimal[10];
        for (var i = 0; i < 10; i++)
        {
            portfolioReturns[i] = 0.8m * mktReturns[i] + 0.5m * hmlReturns[i] + 0.001m;
        }

        var factorNames = new[] { "Mkt-Rf", "HML" };
        var factorReturns = new[] { mktReturns, hmlReturns };

        var result = FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        result.FactorLoadings["HML"].Should().BeGreaterThan(0m,
            "Value-tilted portfolio should have positive HML loading");
        result.FactorLoadings["Mkt-Rf"].Should().BeGreaterThan(0m,
            "Value-tilted portfolio should have positive market loading");
    }

    // --- RP3-02 Test: Single observation throws ---

    [Fact]
    public void Regress_InsufficientData_ShouldThrow()
    {
        var portfolioReturns = new[] { 0.01m };
        var factorNames = new[] { "Mkt-Rf" };
        var factorReturns = new[] { new[] { 0.01m } };

        var act = () => FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        act.Should().Throw<ArgumentException>();
    }

    // --- RP3-02 Test: Mismatched lengths throw ---

    [Fact]
    public void Regress_MismatchedLengths_ShouldThrow()
    {
        var portfolioReturns = new[] { 0.01m, 0.02m, 0.03m };
        var factorNames = new[] { "Mkt-Rf" };
        var factorReturns = new[] { new[] { 0.01m, 0.02m } }; // Length mismatch

        var act = () => FactorRegressor.Regress(portfolioReturns, factorNames, factorReturns);

        act.Should().Throw<ArgumentException>();
    }
}

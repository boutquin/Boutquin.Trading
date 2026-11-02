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

using System.Numerics;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.Trading.Application.PortfolioConstruction;
using Boutquin.Trading.Domain.Interfaces;
using R = Boutquin.Trading.Application.PortfolioConstruction.CertifiedMaximumDiversification.Rational;

public sealed class MaximumDiversificationCertificateTests
{
    [Fact]
    public void ComputeTargetWeights_BindingBounds_RepairsOriginalNativeWitness()
    {
        decimal[,] covariance = { { 1m, 1.90m, .45m, .20m }, { 1.90m, 4m, 1.2m, .8m }, { .45m, 1.2m, 9m, 3.6m }, { .20m, .8m, 3.6m, 16m } };
        var weights = Run(covariance, .1m, .4m);
        Assert.Equal(R.One, weights.Aggregate(R.Zero, (sum, w) => sum + R.From(w)));
        Assert.All(weights, w => Assert.InRange(w, .1m, .4m));
        // Independent active-face oracle: DR*² = 90320/40539. Compare squared
        // ratios on exact emitted decimal coefficients, without the production certificate.
        var units = weights.Select(ToLatticeUnits).ToArray();
        var numerator = units.Select((w, i) => w * (i + 1)).Aggregate(BigInteger.Zero, (sum, w) => sum + w);
        var varianceTimes100 = BigInteger.Zero;
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                varianceTimes100 += units[i] * new BigInteger(100m * covariance[i, j]) * units[j];
            }
        }

        Assert.True(90320 * varianceTimes100 * BigInteger.Pow(10, 20) <=
            40539 * 100 * BigInteger.Pow(new BigInteger(10000000001), 2) * numerator * numerator);
    }

    [Fact]
    public void HasCertificate_PositiveLowerBoundWithExcessiveLoss_Rejects()
    {
        Assert.False(CertifiedMaximumDiversification.HasCertificate(new decimal[,] { { 1m, 0m }, { 0m, 4m } }, [1m, 2m], [.67m, .33m], 0m, 1m));
    }

    [Fact]
    public void HasCertificate_NonzeroExactBudgetOrBoundViolation_Rejects()
    {
        decimal[,] c = { { 1m, 0m }, { 0m, 4m } };
        Assert.False(CertifiedMaximumDiversification.HasCertificate(c, [1m, 2m], [.6666666666666666666666666667m, .3333333333333333333333333334m], 0m, 1m));
        Assert.False(CertifiedMaximumDiversification.HasCertificate(c, [1m, 2m], [.6000000000000000000000000001m, .3999999999999999999999999999m], 0m, .6m));
    }

    [Fact]
    public void RestoreFeasibility_Thirds_EmitsExactBudgetInsideAuthoredBox()
    {
        var w = CertifiedMaximumDiversification.RestoreFeasibility([1m / 3m, 1m / 3m, 1m / 3m], .1m, .4m);
        Assert.Equal(R.One, w.Aggregate(R.Zero, (sum, value) => sum + R.From(value)));
        Assert.All(w, value => Assert.InRange(value, .1m, .4m));
        Assert.Equal(.3333333333333333333333333334m, w[0]);
    }

    [Fact]
    public void TrySolve_OriginalWitness_RecordsBoundedWorkAndRestoration()
    {
        decimal[,] c = { { 1m, 1.90m, .45m, .20m }, { 1.90m, 4m, 1.2m, .8m }, { .45m, 1.2m, 9m, 3.6m }, { .20m, .8m, 3.6m, 16m } };
        Assert.True(CertifiedMaximumDiversification.TrySolve(c, [1m, 2m, 3m, 4m], .1m, .4m, out var w, out var trace));
        Assert.InRange(trace.QpCalls, 1, 257);
        Assert.NotEmpty(trace.RestorationL1Changes);
        Assert.True(CertifiedMaximumDiversification.HasCertificate(c, [1m, 2m, 3m, 4m], w, .1m, .4m));
    }

    [Fact]
    public void ToPositiveDecimal_NearestTiesEvenAndUnderflow_UsesFrozenPolicy()
    {
        var denominator = BigInteger.Pow(10, 29);
        Assert.Equal(.0000000000000000000000000002m, new R(15, denominator).ToPositiveDecimal());
        Assert.Equal(.0000000000000000000000000002m, new R(25, denominator).ToPositiveDecimal());
        Assert.Throws<InvalidOperationException>(() => new R(5, denominator).ToPositiveDecimal());
        Assert.Throws<InvalidOperationException>(() => (R.From(decimal.MaxValue) + R.One).ToPositiveDecimal());
        Assert.Equal(decimal.MaxValue, R.From(decimal.MaxValue).ToPositiveDecimal());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(17)]
    public void ComputeTargetWeights_Diagonal_GenericNAndAsOfIdentity(int n)
    {
        var covariance = new decimal[n, n];
        for (var i = 0; i < n; i++)
        {
            covariance[i, i] = (i + 1) * (i + 1);
        }

        var assets = Enumerable.Range(0, n).Select(i => new Symbol("S" + i)).ToArray();
        var returns = Enumerable.Range(0, n).Select(_ => new[] { .01m, -.01m }).ToArray();
        IPortfolioConstructionModel model = new MaximumDiversificationConstruction(new FixedEstimator(covariance));
        var actual = model.ComputeTargetWeights(assets, returns);
        var dated = model.ComputeTargetWeights(assets, returns, new DateOnly(2026, 10, 28));
        Assert.Equal(actual.Values, dated.Values);
        Assert.Equal(R.One, actual.Values.Aggregate(R.Zero, (sum, w) => sum + R.From(w)));
    }

    [Fact]
    public void ComputeTargetWeights_ExternalDomain_PreservesLegacyRoutes()
    {
        Assert.Equal(new[] { .5m, .5m }, Run(new decimal[,] { { 1m, 0m }, { 0m, 4m } }, 0m, .25m));
        Assert.Equal(new[] { .5m, .5m }, Run(new decimal[,] { { 1m, 1m }, { 1m, 1m } }));
        Assert.Throws<Boutquin.Trading.Domain.Exceptions.CalculationException>(() => Run(new decimal[,] { { 0m, 0m }, { 0m, 1m } }));
        Assert.Throws<OverflowException>(() => Run(new decimal[,] { { -1m, 0m }, { 0m, 1m } }));
    }

    private static decimal[] Run(decimal[,] covariance, decimal lower = 0m, decimal upper = 1m)
    {
        var n = covariance.GetLength(0);
        var assets = Enumerable.Range(0, n).Select(i => new Symbol("S" + i)).ToArray();
        var returns = Enumerable.Range(0, n).Select(_ => new[] { .01m, -.01m }).ToArray();
        var result = new MaximumDiversificationConstruction(new FixedEstimator(covariance), lower, upper).ComputeTargetWeights(assets, returns);
        return assets.Select(s => result[s]).ToArray();
    }

    private static BigInteger ToLatticeUnits(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | (uint)bits[0];
        var scale = (bits[3] >> 16) & 0xff;
        return (bits[3] < 0 ? -coefficient : coefficient) * BigInteger.Pow(10, 28 - scale);
    }

    private sealed class FixedEstimator(decimal[,] covariance) : ICovarianceEstimator
    {
        public decimal[,] Estimate(decimal[][] returns) => (decimal[,])covariance.Clone();
    }
}

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

using Boutquin.Trading.Application.Indicators;

public sealed class IndicatorTests
{
    private const decimal Precision = 1e-6m;

    // ============================================================
    // SimpleMovingAverage Tests
    // ============================================================

    [Fact]
    public void SMA_Compute_ShouldMatchKnownValues()
    {
        // SMA(5) of [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] = average of last 5 = (6+7+8+9+10)/5 = 8
        var sma = new SimpleMovingAverage(5);
        var values = new[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m };
        sma.Compute(values).Should().Be(8m);
    }

    [Fact]
    public void SMA_Compute_PeriodEqualsLength_ShouldReturnFullAverage()
    {
        var sma = new SimpleMovingAverage(3);
        var values = new[] { 10m, 20m, 30m };
        sma.Compute(values).Should().Be(20m);
    }

    [Fact]
    public void SMA_Compute_Period1_ShouldReturnLastValue()
    {
        var sma = new SimpleMovingAverage(1);
        var values = new[] { 5m, 10m, 15m };
        sma.Compute(values).Should().Be(15m);
    }

    [Fact]
    public void SMA_Compute_InsufficientData_ShouldThrow()
    {
        var sma = new SimpleMovingAverage(10);
        var values = new[] { 1m, 2m, 3m };
        var act = () => sma.Compute(values);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void SMA_Compute_NullArray_ShouldThrow()
    {
        var sma = new SimpleMovingAverage(5);
        var act = () => sma.Compute(null!);
        act.Should().Throw<EmptyOrNullArrayException>();
    }

    [Fact]
    public void SMA_Constructor_ZeroPeriod_ShouldThrow()
    {
        var act = () => new SimpleMovingAverage(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // ExponentialMovingAverage Tests
    // ============================================================

    [Fact]
    public void EMA_Compute_ShouldWeightRecentDataMore()
    {
        // EMA(3) with multiplier = 2/(3+1) = 0.5
        // Values: [10, 20, 30, 40]
        // Seed (SMA of first 3): (10+20+30)/3 = 20
        // EMA after 40: (40 - 20) * 0.5 + 20 = 30
        var ema = new ExponentialMovingAverage(3);
        var values = new[] { 10m, 20m, 30m, 40m };
        ema.Compute(values).Should().BeApproximately(30m, Precision);
    }

    [Fact]
    public void EMA_Compute_ExactlyPeriodValues_ShouldReturnSMA()
    {
        // When length == period, EMA is just SMA (no additional values to apply multiplier)
        var ema = new ExponentialMovingAverage(3);
        var values = new[] { 10m, 20m, 30m };
        ema.Compute(values).Should().Be(20m);
    }

    [Fact]
    public void EMA_Compute_WarmUp_ShouldBeCorrect()
    {
        // EMA(5) with multiplier = 2/6 = 1/3
        // Values: [2, 4, 6, 8, 10, 12]
        // Seed: (2+4+6+8+10)/5 = 6
        // After 12: (12 - 6) * (1/3) + 6 = 8
        var ema = new ExponentialMovingAverage(5);
        var values = new[] { 2m, 4m, 6m, 8m, 10m, 12m };
        ema.Compute(values).Should().BeApproximately(8m, Precision);
    }

    [Fact]
    public void EMA_Compute_InsufficientData_ShouldThrow()
    {
        var ema = new ExponentialMovingAverage(10);
        var values = new[] { 1m, 2m };
        var act = () => ema.Compute(values);
        act.Should().Throw<InsufficientDataException>();
    }

    // ============================================================
    // RealizedVolatility Tests
    // ============================================================

    [Fact]
    public void RealizedVol_Compute_ShouldAnnualize()
    {
        // Constant returns → zero vol
        // But actually constant returns have zero std dev which is valid
        // Use known returns: [0.01, -0.01, 0.01, -0.01, 0.01]
        var rv = new RealizedVolatility(5);
        var returns = new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m };
        var result = rv.Compute(returns);

        // Mean = 0.002, deviations from mean: 0.008, -0.012, 0.008, -0.012, 0.008
        // Sample variance = sum of squared deviations / 4
        // Manual: (0.008^2 + 0.012^2 + 0.008^2 + 0.012^2 + 0.008^2) / 4
        //       = (0.000064 + 0.000144 + 0.000064 + 0.000144 + 0.000064) / 4
        //       = 0.000480 / 4 = 0.000120
        // daily stddev = sqrt(0.00012) ≈ 0.010954
        // annualized = 0.010954 * sqrt(252) ≈ 0.173861
        result.Should().BeApproximately(0.173861m, 0.001m);
    }

    [Fact]
    public void RealizedVol_Compute_UsesLastWindowReturns()
    {
        // 10 returns but window=5 → only last 5 used
        var rv = new RealizedVolatility(5);
        var returns = new[] { 0.1m, 0.2m, 0.3m, 0.4m, 0.5m, 0.01m, -0.01m, 0.01m, -0.01m, 0.01m };
        var resultFromAll = rv.Compute(returns);

        // Same as computing on just the last 5
        var last5 = new[] { 0.01m, -0.01m, 0.01m, -0.01m, 0.01m };
        var resultFromLast5 = rv.Compute(last5);

        resultFromAll.Should().BeApproximately(resultFromLast5, Precision);
    }

    [Fact]
    public void RealizedVol_Compute_InsufficientData_ShouldThrow()
    {
        var rv = new RealizedVolatility(20);
        var returns = new[] { 0.01m, -0.01m };
        var act = () => rv.Compute(returns);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void RealizedVol_Constructor_WindowTooSmall_ShouldThrow()
    {
        var act = () => new RealizedVolatility(1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ============================================================
    // MomentumScore Tests
    // ============================================================

    [Fact]
    public void Momentum_Compute_ShouldExcludeMostRecentMonth()
    {
        // 12-1 month momentum with 21 days/month → needs 252 daily returns
        // Create 252 returns that are all 0.001 (positive)
        // Momentum should be cumulative return from day 0..230 (= 11 months * 21 days)
        var momentum = new MomentumScore(12, 1, 21);
        var returns = Enumerable.Repeat(0.001m, 252).ToArray();
        var result = momentum.Compute(returns);

        // 231 days of 0.1% daily return: (1.001)^231 - 1 ≈ 0.2597
        var expected = (decimal)Math.Pow(1.001, 231) - 1m;
        result.Should().BeApproximately(expected, 0.001m);
    }

    [Fact]
    public void Momentum_Compute_InsufficientData_ShouldThrow()
    {
        var momentum = new MomentumScore(12, 1, 21);
        var returns = new decimal[100]; // Need 252
        var act = () => momentum.Compute(returns);
        act.Should().Throw<InsufficientDataException>();
    }

    [Fact]
    public void Momentum_Constructor_SkipGETotal_ShouldThrow()
    {
        var act = () => new MomentumScore(totalMonths: 6, skipMonths: 6);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Momentum_Compute_AllZeroReturns_ShouldReturnZero()
    {
        var momentum = new MomentumScore(12, 1, 21);
        var returns = new decimal[252]; // All zeros
        momentum.Compute(returns).Should().Be(0m);
    }
}

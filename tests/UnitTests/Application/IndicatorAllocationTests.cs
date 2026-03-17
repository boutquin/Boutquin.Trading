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

public sealed class IndicatorAllocationTests
{
    private const decimal Precision = 1e-12m;

    [Theory]
    [MemberData(nameof(IndicatorAllocationTestData.SmaCases),
        MemberType = typeof(IndicatorAllocationTestData))]
    public void SMA_Compute_SameResultAfterOptimization(decimal[] values, int period, decimal expected)
    {
        var sma = new SimpleMovingAverage(period);
        var result = sma.Compute(values);

        result.Should().BeApproximately(expected, Precision);
    }

    [Theory]
    [MemberData(nameof(IndicatorAllocationTestData.EmaCases),
        MemberType = typeof(IndicatorAllocationTestData))]
    public void EMA_Compute_SameResultAfterOptimization(decimal[] values, int period)
    {
        var ema = new ExponentialMovingAverage(period);
        var result = ema.Compute(values);

        // Compute expected EMA manually
        var multiplier = 2m / (period + 1);
        var seed = 0m;
        for (var i = 0; i < period; i++)
        {
            seed += values[i];
        }

        seed /= period;

        var expected = seed;
        for (var i = period; i < values.Length; i++)
        {
            expected = (values[i] - expected) * multiplier + expected;
        }

        result.Should().BeApproximately(expected, Precision);
    }

    [Theory]
    [MemberData(nameof(IndicatorAllocationTestData.RealizedVolCases),
        MemberType = typeof(IndicatorAllocationTestData))]
    public void RealizedVolatility_Compute_SameResultAfterOptimization(decimal[] returns, int window)
    {
        var rv = new RealizedVolatility(window);
        var result = rv.Compute(returns);

        // Compute expected from last 'window' returns
        var windowReturns = returns.AsSpan(returns.Length - window).ToArray();
        var mean = windowReturns.Average();
        var sumSqDev = windowReturns.Sum(r => (r - mean) * (r - mean));
        var variance = sumSqDev / (window - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);
        var expected = stdDev * (decimal)Math.Sqrt(252);

        result.Should().BeApproximately(expected, Precision);
    }
}

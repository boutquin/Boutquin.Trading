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

namespace Boutquin.Trading.Application.Indicators;

public sealed class RealizedVolatility : IIndicator
{
    private const int DefaultTradingDaysPerYear = 252;
    private readonly int _window;
    private readonly int _tradingDaysPerYear;

    public RealizedVolatility(int window, int tradingDaysPerYear = DefaultTradingDaysPerYear)
    {
        if (window < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be at least 2 for volatility calculation.");
        }

        Guard.AgainstNegativeOrZero(() => tradingDaysPerYear);
        _window = window;
        _tradingDaysPerYear = tradingDaysPerYear;
    }

    public decimal Compute(decimal[] returns)
    {
        Guard.AgainstNullOrEmptyArray(() => returns);
        if (returns.Length < _window)
        {
            throw new InsufficientDataException(
                $"Need at least {_window} returns to compute realized volatility, got {returns.Length}.");
        }

        // Compute mean of last _window returns (allocation-free)
        var start = returns.Length - _window;
        var mean = 0m;
        for (var i = start; i < returns.Length; i++)
        {
            mean += returns[i];
        }

        mean /= _window;

        // Compute sample variance
        var sumSquaredDev = 0m;
        for (var i = start; i < returns.Length; i++)
        {
            var dev = returns[i] - mean;
            sumSquaredDev += dev * dev;
        }

        var variance = sumSquaredDev / (_window - 1); // sample variance
        var stdDev = (decimal)Math.Sqrt((double)variance);

        return stdDev * (decimal)Math.Sqrt(_tradingDaysPerYear);
    }
}

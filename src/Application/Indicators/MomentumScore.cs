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

/// <summary>
/// Computes cross-sectional momentum as the cumulative return over a trailing window,
/// excluding the most recent month(s) to avoid short-term reversal (Jegadeesh &amp; Titman, 1993).
/// Default: 12-1 month momentum (12 months total, skip last 1 month).
/// </summary>
public sealed class MomentumScore : IIndicator
{
    private readonly int _totalMonths;
    private readonly int _skipMonths;
    private readonly int _tradingDaysPerMonth;

    /// <summary>
    /// Initializes a new instance of the <see cref="MomentumScore"/> class.
    /// </summary>
    /// <param name="totalMonths">Total lookback in months. Must be positive.</param>
    /// <param name="skipMonths">Recent months to exclude (short-term reversal). Must be positive and less than <paramref name="totalMonths"/>.</param>
    /// <param name="tradingDaysPerMonth">Trading days per month for converting months to daily indices. Must be positive.</param>
    public MomentumScore(int totalMonths = 12, int skipMonths = 1, int tradingDaysPerMonth = 21)
    {
        Guard.AgainstNegativeOrZero(() => totalMonths);
        Guard.AgainstNegativeOrZero(() => skipMonths);
        Guard.AgainstNegativeOrZero(() => tradingDaysPerMonth);

        if (skipMonths >= totalMonths)
        {
            throw new ArgumentException("skipMonths must be less than totalMonths.", nameof(skipMonths));
        }

        _totalMonths = totalMonths;
        _skipMonths = skipMonths;
        _tradingDaysPerMonth = tradingDaysPerMonth;
    }

    /// <inheritdoc />
    public decimal Compute(decimal[] dailyReturns)
    {
        Guard.AgainstNullOrEmptyArray(() => dailyReturns);

        var requiredDays = _totalMonths * _tradingDaysPerMonth;
        if (dailyReturns.Length < requiredDays)
        {
            throw new InsufficientDataException(
                $"Need at least {requiredDays} daily returns for {_totalMonths}-{_skipMonths} momentum, got {dailyReturns.Length}.");
        }

        var skipDays = _skipMonths * _tradingDaysPerMonth;

        // Window: from (end - totalMonths*dpm) to (end - skipMonths*dpm)
        var startIndex = dailyReturns.Length - requiredDays;
        var endIndex = dailyReturns.Length - skipDays;

        var cumulativeReturn = 1m;
        for (var i = startIndex; i < endIndex; i++)
        {
            cumulativeReturn *= (1m + dailyReturns[i]);
        }

        return cumulativeReturn - 1m;
    }
}

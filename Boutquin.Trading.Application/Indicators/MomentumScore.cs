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

public sealed class MomentumScore : IIndicator
{
    private readonly int _totalMonths;
    private readonly int _skipMonths;
    private readonly int _tradingDaysPerMonth;

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

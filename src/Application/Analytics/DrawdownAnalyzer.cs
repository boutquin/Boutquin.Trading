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

using Boutquin.Trading.Domain.Analytics;
using Boutquin.Trading.Domain.Exceptions;

namespace Boutquin.Trading.Application.Analytics;

/// <summary>
/// Analyzes an equity curve to produce a detailed list of drawdown periods
/// with start, trough, recovery dates, depth, and duration.
/// </summary>
public static class DrawdownAnalyzer
{
    /// <summary>
    /// Identifies all distinct drawdown periods in an equity curve.
    /// </summary>
    /// <param name="equityCurve">A sorted dictionary of date → equity value.</param>
    /// <returns>A list of <see cref="DrawdownPeriod"/> records ordered chronologically.</returns>
    public static IReadOnlyList<DrawdownPeriod> AnalyzeDrawdownPeriods(
        SortedDictionary<DateOnly, decimal> equityCurve)
    {
        if (equityCurve.Count == 0)
        {
            throw new ArgumentException("Equity curve must contain at least one data point.", nameof(equityCurve));
        }

        var periods = new List<DrawdownPeriod>();
        var entries = equityCurve.ToList();

        var peak = entries[0].Value;
        if (peak == 0m)
        {
            throw new CalculationException("Cannot compute drawdown when peak equity is zero.");
        }

        var peakDate = entries[0].Key;
        var inDrawdown = false;
        var troughValue = peak;
        var troughDate = peakDate;

        for (var i = 1; i < entries.Count; i++)
        {
            var date = entries[i].Key;
            var value = entries[i].Value;

            if (value >= peak)
            {
                // If we were in a drawdown, this is the recovery point
                if (inDrawdown)
                {
                    var depth = (troughValue - peak) / peak;
                    var durationDays = (date.ToDateTime(TimeOnly.MinValue) - peakDate.ToDateTime(TimeOnly.MinValue)).Days;
                    var recoveryDays = (date.ToDateTime(TimeOnly.MinValue) - troughDate.ToDateTime(TimeOnly.MinValue)).Days;

                    periods.Add(new DrawdownPeriod(
                        StartDate: peakDate,
                        TroughDate: troughDate,
                        RecoveryDate: date,
                        Depth: depth,
                        DurationDays: durationDays,
                        RecoveryDays: recoveryDays));

                    inDrawdown = false;
                }

                peak = value;
                peakDate = date;
                troughValue = value;
                troughDate = date;
            }
            else
            {
                // We are in drawdown territory
                inDrawdown = true;

                if (value < troughValue)
                {
                    troughValue = value;
                    troughDate = date;
                }
            }
        }

        // If we end in a drawdown (no recovery)
        if (inDrawdown)
        {
            var lastDate = entries[^1].Key;
            var depth = (troughValue - peak) / peak;
            var durationDays = (lastDate.ToDateTime(TimeOnly.MinValue) - peakDate.ToDateTime(TimeOnly.MinValue)).Days;

            periods.Add(new DrawdownPeriod(
                StartDate: peakDate,
                TroughDate: troughDate,
                RecoveryDate: null,
                Depth: depth,
                DurationDays: durationDays,
                RecoveryDays: null));
        }

        return periods;
    }
}

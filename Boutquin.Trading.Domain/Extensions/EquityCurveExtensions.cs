// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

namespace Boutquin.Trading.Domain.Extensions;
using Boutquin.Domain.Exceptions;

/// <summary>
/// Provides extension methods for calculating drawdowns from an
/// equity curve represented by a SortedDictionary&lt;DateTime, decimal&gt;.
/// </summary>
public static class EquityCurveExtensions
{
    /// <summary>
    /// Calculates the drawdowns, maximum drawdown, and its duration from the given equity curve.
    /// </summary>
    /// <param name="equityCurve">A SortedDictionary&lt;DateTime, decimal&gt; representing the equity curve of a trading strategy.</param>
    /// <returns>A tuple with the drawdowns as SortedDictionary&lt;DateTime, decimal&gt;, the maximum drawdown as decimal, and its duration as int.</returns>
    /// <exception cref="EmptyOrNullDictionaryException">Thrown when the <paramref name="equityCurve"/> is null or empty.</exception>
    /// <exception cref="InsufficientDataException">Thrown when the <paramref name="equityCurve"/> contains less than two elements for sample calculation.</exception>
    /// <example>
    /// <code>
    /// var equityCurve = new SortedDictionary&lt;DateTime, decimal&gt; {
    ///     { new DateTime(2021, 1, 1), 1000m },
    ///     { new DateTime(2021, 1, 2), 1020m },
    ///     { new DateTime(2021, 1, 3), 1010m },
    ///     { new DateTime(2021, 1, 4), 1030m },
    /// };
    ///
    /// var (drawdowns, maxDrawdown, maxDrawdownDuration) = equityCurve.DrawdownAnalysis();
    /// </code>
    /// </example>
    public static (SortedDictionary<DateOnly, decimal> Drawdowns, decimal MaxDrawdown, int MaxDrawdownDuration) 
        CalculateDrawdownsAndMaxDrawdownInfo(this IReadOnlyDictionary<DateOnly, decimal> equityCurve)
    {
        // Ensure the equity curve dictionary is not null or empty
        Guard.AgainstEmptyOrNullReadOnlyDictionary(() => equityCurve);
        // Check if there is enough data for sample calculation
        Guard.Against(equityCurve.Count == 1)
            .With<InsufficientDataException>(ExceptionMessages.InsufficientDataForSampleCalculation);

        // Initialize a SortedDictionary to store the drawdowns.
        var drawdowns = new SortedDictionary<DateOnly, decimal>();

        // Initialize variables for peak equity, maximum drawdown, drawdown duration, and maximum drawdown duration.
        decimal peakEquity = 0;
        decimal maxDrawdown = 0;
        var maxDrawdownDuration = 0;

        // Initialize a variable to store the start date of the current drawdown.
        var startDrawdownDate = DateOnly.MinValue;

        // Create a TimeSpan for the desired time of day, 00:00:00 (midnight)
        var timeOfDay = new TimeOnly(0, 0, 0);
        // Iterate through the given equity curve.
        foreach (var (date, equity) in equityCurve)
        {
            // Get the date and equity value at each data point.

            // If a new highest equity value is encountered, update the peak equity and reset the drawdown duration.
            var drawdownDuration = 0;
            if (equity > peakEquity)
            {
                peakEquity = equity;
                startDrawdownDate = date;
                drawdownDuration = 0;
            }
            else
            {
                // Otherwise, update the drawdown duration and check if it's the longest drawdown duration encountered so far.
                drawdownDuration = (date.ToDateTime(timeOfDay) - startDrawdownDate.ToDateTime(timeOfDay)).Days + 1;
                if (drawdownDuration > maxDrawdownDuration)
                {
                    maxDrawdownDuration = drawdownDuration;
                }
            }

            // Calculate the drawdown by dividing the current equity value by the peak value and subtracting 1.
            var drawdown = equity > peakEquity ? 0 : (equity / peakEquity) - 1;

            // Update the maximum drawdown if the current drawdown is greater than the previous maximum drawdown.
            if (drawdown < maxDrawdown)
            {
                maxDrawdown = drawdown;
            }

            // Add the calculated drawdown to the drawdowns SortedDictionary with the corresponding date.
            drawdowns[date] = drawdown;
        }

        // Return the calculated drawdowns, maximum drawdown, and maximum drawdown duration.
        return (drawdowns, maxDrawdown, maxDrawdownDuration);
    }
}

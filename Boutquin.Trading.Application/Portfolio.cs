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

using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Application;

/// <summary>
/// Represents a trading portfolio that consists of multiple strategies and assets.
/// The Portfolio class is responsible for managing the assets, positions, capital allocation,
/// and risk management for the strategies in the portfolio. It also handles various types
/// of events such as market, dividend, signal, order, and fill events and updates the portfolio
/// state accordingly. The Portfolio class maintains an equity curve that represents the value
/// of the portfolio over time.
/// </summary>
public sealed class Portfolio
{
    /// <summary>
    /// Retrieves the portfolio's equity curve, represented as a SortedDictionary with DateTime keys and decimal values.
    /// The equity curve represents the value of the portfolio over time, where the keys are the timestamps of events
    /// and the values are the total equity at each timestamp.
    /// </summary>
    public SortedDictionary<DateTime, decimal> EquityCurve { get; } = new();

    /// <summary>
    /// Retrieves the list of trading strategies in the portfolio.
    /// </summary>
    public List<IStrategy> Strategies { get; }

    /// <summary>
    /// Initializes a new instance of the Portfolio class with a list of trading strategies.
    /// </summary>
    /// <param name="strategies">A list of objects implementing the IStrategy interface, representing the trading strategies in the portfolio.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided strategies list is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided strategies list is empty.</exception>
    public Portfolio(List<IStrategy> strategies)
    {
        Strategies = strategies ?? throw new ArgumentNullException(nameof(strategies), "The provided strategies list cannot be null.");
        if (Strategies.Count == 0)
        {
            throw new ArgumentException("The provided strategies list cannot be empty.", nameof(strategies));
        }
    }

    /// <summary>
    /// Updates the equity curve of the portfolio at a specific timestamp. The method calculates the total equity
    /// at the given timestamp and adds an entry to the EquityCurve SortedDictionary.
    /// </summary>
    /// <param name="timestamp">The DateTime representing the timestamp at which the equity curve should be updated.</param>
    /// <exception cref="ArgumentException">Thrown when the provided timestamp is earlier than the last entry in the equity curve.</exception>
    public void UpdateEquityCurve(DateTime timestamp)
    {
        if (EquityCurve.Count > 0 && timestamp < EquityCurve.Keys.Last())
        {
            throw new ArgumentException("Timestamp must be equal to or greater than the last entry in the equity curve.", nameof(timestamp));
        }

        var totalEquity = CalculateTotalEquity();
        EquityCurve[timestamp] = totalEquity;
    }

    /// <summary>
    /// Processes an event and forwards it to the appropriate strategies.
    /// </summary>
    /// <param name="e">The event to be processed.</param>
    public void HandleEvent(IEvent e)
    {
        foreach (var strategy in Strategies)
        {
            strategy.OnEvent(e);
        }
    }

    /// <summary>
    /// Calculates the total equity of the portfolio by summing the equities of all the strategies in the portfolio.
    /// </summary>
    /// <returns>A decimal value representing the total equity of the portfolio.</returns>
    public decimal CalculateTotalEquity()
    {
        if (Strategies.Count == 0)
        {
            throw new InvalidOperationException("The portfolio must have at least one strategy.");
        }

        var totalEquity = Strategies.Sum(strategy => strategy.CalculateEquity());
        return totalEquity;
    }
}

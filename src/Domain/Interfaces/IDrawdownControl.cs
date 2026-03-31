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

namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// Daily drawdown monitoring and circuit-breaker control for backtests.
/// Called once per trading day after the equity curve is updated.
/// Implementations may liquidate positions when drawdown exceeds a threshold.
/// </summary>
public interface IDrawdownControl
{
    /// <summary>
    /// Checks the portfolio's current drawdown and takes action if thresholds are breached.
    /// Called daily in the backtest loop after <see cref="IPortfolio.UpdateEquityCurve"/>.
    /// </summary>
    /// <param name="portfolio">The portfolio to monitor.</param>
    /// <param name="date">The current trading date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CheckAsync(IPortfolio portfolio, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of times the circuit breaker has triggered a full liquidation.
    /// </summary>
    int LiquidationCount { get; }

    /// <summary>
    /// Whether the portfolio is currently in cash-only mode after a circuit breaker trigger.
    /// </summary>
    bool InCashMode { get; }
}

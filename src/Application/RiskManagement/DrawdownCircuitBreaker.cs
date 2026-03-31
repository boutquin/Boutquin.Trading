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

namespace Boutquin.Trading.Application.RiskManagement;

using Domain.Enums;
using Domain.Events;

/// <summary>
/// Daily drawdown circuit breaker that liquidates all positions when drawdown
/// exceeds a configured threshold, then holds cash for a cooldown period
/// before allowing the normal rebalancing strategy to re-enter.
///
/// Liquidation orders are submitted directly via <see cref="IPortfolio.SubmitOrderAsync"/>
/// (bypassing risk rules) and fill at the next bar's Open price.
/// </summary>
public sealed class DrawdownCircuitBreaker : IDrawdownControl
{
    /// <summary>
    /// Tolerance for drawdown comparison (1 basis point = 0.01%).
    /// Equity curve values are computed from integer shares × prices;
    /// (peak − current) / peak can land within a sub-bp epsilon of the limit.
    /// </summary>
    private const decimal Tolerance = 0.0001m;

    private readonly decimal _maxDrawdownPercent;
    private readonly int _cooldownDays;

    private decimal _peakValue;
    private bool _peakInitialized;
    private bool _inCashMode;
    private int _cooldownCounter;
    private int _liquidationCount;

    /// <param name="maxDrawdownPercent">Maximum drawdown as a decimal (e.g., 0.25 for 25%).</param>
    /// <param name="cooldownDays">Trading days to remain in cash after liquidation (default 21).</param>
    public DrawdownCircuitBreaker(decimal maxDrawdownPercent, int cooldownDays = 21)
    {
        if (maxDrawdownPercent is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDrawdownPercent), maxDrawdownPercent,
                "Must be between 0 (exclusive) and 1 (inclusive).");
        }

        if (cooldownDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldownDays), cooldownDays,
                "Must be >= 0.");
        }

        _maxDrawdownPercent = maxDrawdownPercent;
        _cooldownDays = cooldownDays;
    }

    /// <inheritdoc />
    public int LiquidationCount => _liquidationCount;

    /// <inheritdoc />
    public bool InCashMode => _inCashMode;

    /// <inheritdoc />
    public async Task CheckAsync(IPortfolio portfolio, DateOnly date, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(() => portfolio);

        if (!portfolio.EquityCurve.TryGetValue(date, out var currentValue) || currentValue <= 0)
        {
            return;
        }

        // Track peak value
        if (!_peakInitialized || currentValue > _peakValue)
        {
            _peakValue = currentValue;
            _peakInitialized = true;
        }

        // During cooldown: count days and exit when expired
        if (_inCashMode)
        {
            _cooldownCounter++;
            if (_cooldownCounter >= _cooldownDays)
            {
                _inCashMode = false;
                _peakValue = currentValue; // Reset baseline for re-entry
            }
            return;
        }

        // Check drawdown
        var drawdown = (_peakValue - currentValue) / _peakValue;
        if (drawdown < _maxDrawdownPercent + Tolerance)
        {
            return;
        }

        // Circuit breaker triggered — liquidate all positions
        foreach (var (strategyName, strategy) in portfolio.Strategies)
        {
            foreach (var (asset, quantity) in strategy.Positions)
            {
                if (quantity <= 0)
                {
                    continue;
                }

                var sellOrder = new OrderEvent(
                    Timestamp: date,
                    StrategyName: strategyName,
                    Asset: asset,
                    TradeAction: TradeAction.Sell,
                    OrderType: OrderType.Market,
                    Quantity: quantity);

                await portfolio.SubmitOrderAsync(sellOrder, cancellationToken).ConfigureAwait(false);
            }
        }

        _inCashMode = true;
        _cooldownCounter = 0;
        _liquidationCount++;
    }
}

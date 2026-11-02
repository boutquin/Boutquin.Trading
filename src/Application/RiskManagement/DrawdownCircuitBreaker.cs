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
/// before allowing the normal rebalancing strategy to re-enter. Current holdings and currency cash
/// are marked at the check date independently of reporting equity and its burn-in window.
/// Nonpositive NAV throws <see cref="InvalidOperationException"/>; missing valuation inputs propagate.
///
/// Liquidation orders are submitted directly via <see cref="IPortfolio.SubmitOrderAsync"/>
/// (bypassing risk rules) and fill at the next bar's Open price. Submission refusal or an
/// exception aborts execution. At the next session, nonzero holdings also abort rather than
/// silently counting an unfilled liquidation as cash. After either failure this instance is
/// non-resumable; reconstruct the backtest from reconciled state before retrying.
/// </summary>
public sealed class DrawdownCircuitBreaker : IDrawdownControl
{
    /// <summary>
    /// Tolerance for drawdown comparison (1 basis point = 0.01%).
    /// Current NAV values are computed from integer shares × prices;
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
    private bool _executionFailed;

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

    /// <summary>Updates daily risk from current marked holdings/cash and, on a breach, submits next-bar liquidation orders.</summary>
    /// <param name="portfolio">The current portfolio; reporting rows are neither read nor added.</param>
    /// <param name="date">The current session's valuation date, after pending fills and historical-data updates.</param>
    /// <param name="cancellationToken">Cancellation forwarded to each submission; interrupted liquidation is non-resumable.</param>
    /// <returns>A task completing after the check and any required submissions, not after their next-bar fills.</returns>
    /// <exception cref="ArgumentNullException">The portfolio is null.</exception>
    /// <exception cref="InvalidOperationException">NAV is nonpositive, required valuation data is unavailable, or liquidation failed or remains incomplete.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested or a submission was canceled.</exception>
    public async Task CheckAsync(IPortfolio portfolio, DateOnly date, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(() => portfolio);
        cancellationToken.ThrowIfCancellationRequested();
        if (_executionFailed)
        {
            throw new InvalidOperationException("A prior liquidation execution failed; this backtest must be restarted from a reconciled state.");
        }

        // Once-per-session callers process pending orders before this check. A holding still
        // present after the trigger's next session is an unfilled liquidation, not cash.
        // Fail explicitly: day orders may have expired, and retrying an unknown broker state
        // could duplicate a close. Do not invent a fill or let cooldown expire while invested.
        if (_inCashMode)
        {
            foreach (var (strategyName, strategy) in portfolio.Strategies)
            {
                foreach (var (asset, quantity) in strategy.Positions)
                {
                    if (quantity == 0)
                    {
                        continue;
                    }

                    _executionFailed = true;
                    throw new InvalidOperationException(FormattableString.Invariant(
                        $"Drawdown liquidation is incomplete on {date:yyyy-MM-dd}: {strategyName}/{asset} still holds {quantity} shares."));
                }
            }
        }

        var currentValue = RiskValuation.PositiveNav(portfolio, date);

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

        // Circuit breaker triggered — flatten all positions to cash.
        // Long positions (qty > 0) are sold; short positions (qty < 0) are covered
        // by buying back |qty|. Zero-quantity entries are skipped.
        try
        {
            foreach (var (strategyName, strategy) in portfolio.Strategies)
            {
                foreach (var (asset, quantity) in strategy.Positions)
                {
                    if (quantity == 0)
                    {
                        continue;
                    }

                    var (tradeAction, orderQuantity) = quantity > 0
                        ? (TradeAction.Sell, quantity)
                        : (TradeAction.Buy, -quantity);

                    var closingOrder = new OrderEvent(
                        Timestamp: date,
                        StrategyName: strategyName,
                        Symbol: asset,
                        TradeAction: tradeAction,
                        OrderType: OrderType.Market,
                        Quantity: orderQuantity);

                    if (!await portfolio.SubmitOrderAsync(closingOrder, cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException($"Drawdown liquidation submission was refused for {strategyName}/{asset}.");
                    }
                }
            }
        }
        catch
        {
            // A previous leg may already be accepted, or an exception may hide acceptance.
            // Propagate the original failure and forbid a later duplicate submission.
            _executionFailed = true;
            throw;
        }

        _inCashMode = true;
        _cooldownCounter = 0;
        _liquidationCount++;
    }
}

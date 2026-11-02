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

using Domain.ValueObjects;

/// <summary>
/// Rejects orders that would cause any single position to exceed a configured percentage of total
/// portfolio value, with the position valued in the portfolio's base currency at the decision-date bar.
/// </summary>
/// <remarks>
/// <para>Risk NAV is calculated from current holdings and all currency cash at the decision date;
/// reporting equity and its burn-in window do not control admission. Nonpositive NAV throws
/// <see cref="InvalidOperationException"/>; unavailable valuation inputs propagate their existing errors.</para>
/// <para><b>Share-granularity tolerance:</b> the cap is enforced to within one share. Position sizing
/// rounds share counts with <see cref="MidpointRounding.AwayFromZero"/>, so a target exactly at the cap
/// overshoots it by up to half a share on roughly half of all rebalances; one 100-unit share of a
/// 100,000 portfolio is 0.1%, ten times the fixed tolerance. The rule therefore rejects only when the
/// projected weight exceeds <c>cap + max(Tolerance, w₁)</c>, where <c>w₁</c> is one share's weight in
/// base currency at the decision-date <c>AdjustedClose</c>. The fixed tolerance stays binding whenever a share
/// is worth less than it, the half-share rounding overshoot always fits inside the bound, and anything
/// more than one share over the cap is rejected. The sizers never round toward the cap.</para>
/// <para><b>Fail-safe under missing data:</b> a single order whose asset has no bar on the decision date is
/// rejected rather than admitted; a batch skips an asset without a bar because it cannot value it.</para>
/// </remarks>
public sealed class MaxPositionSizeRule : IRiskRule
{
    /// <summary>
    /// Floor of the comparison tolerance (1 basis point = 0.01%). The tolerance actually applied is
    /// <c>max(Tolerance, one share's weight)</c> — see the class remarks.
    /// </summary>
    private const decimal Tolerance = 0.0001m;

    private readonly decimal _maxPositionPercent;

    /// <summary>
    /// Initializes a new instance of <see cref="MaxPositionSizeRule"/>.
    /// </summary>
    /// <param name="maxPositionPercent">
    /// The maximum allowable position size as a fraction of portfolio value (e.g., 0.25 for 25%).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxPositionPercent"/> is not between 0 (exclusive) and 1 (inclusive).
    /// </exception>
    public MaxPositionSizeRule(decimal maxPositionPercent)
    {
        if (maxPositionPercent is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPositionPercent),
                maxPositionPercent,
                "Max position percent must be between 0 (exclusive) and 1 (inclusive).");
        }

        _maxPositionPercent = maxPositionPercent;
    }

    /// <inheritdoc />
    public string Name => "MaxPositionSize";

    /// <summary>Checks the proposed exposure using current holdings and currency cash valued at the order's decision date.</summary>
    /// <param name="order">The proposed order, whose timestamp selects the marks and FX.</param>
    /// <param name="portfolio">The current portfolio; reporting equity is neither read nor changed.</param>
    /// <returns>The existing cap/tolerance decision or a missing-input rejection.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">Current NAV is nonpositive, or a required valuation input is unavailable.</exception>
    public RiskEvaluation Evaluate(Order order, IPortfolio portfolio)
    {
        Guard.AgainstNull(() => order);
        Guard.AgainstNull(() => portfolio);

        var date = order.Timestamp;
        var totalPortfolioValue = RiskValuation.PositiveNav(portfolio, date);

        // Estimate the value of the proposed order.
        // Missing market data means we cannot evaluate the rule; reject rather than
        // silently admit (consistent with MaxSectorExposureRule). Fail-safe under
        // uncertainty: better to skip an order than to bypass risk limits.
        portfolio.HistoricalMarketData.TryGetValue(date, out var latestMarketData);
        if (latestMarketData is null || !latestMarketData.TryGetValue(order.Symbol, out var marketData))
        {
            return RiskEvaluation.Rejected(
                $"No market data available for {order.Symbol}; cannot evaluate position size.");
        }

        // Get existing position value for this asset across all strategies
        var existingQuantity = 0;
        foreach (var strategy in portfolio.Strategies.Values)
        {
            if (strategy.Positions.TryGetValue(order.Symbol, out var qty))
            {
                existingQuantity += qty;
            }
        }

        // Compute the post-order position value using AdjustedClose for consistency
        // with the rest of the engine (equity curve, position sizer, strategy all use AdjustedClose).
        // Using raw Close causes spurious rejections when dividend-adjusted data diverges.
        var orderQuantity = order.TradeAction == TradeAction.Buy
            ? order.Quantity
            : -order.Quantity;
        var postOrderQuantity = existingQuantity + orderQuantity;

        return CheckProjectedPosition(portfolio, date, order.Symbol, postOrderQuantity, marketData.AdjustedClose, totalPortfolioValue, when: string.Empty);
    }

    /// <summary>
    /// Evaluates the projected position sizes after ALL orders in the batch.
    /// Computes the net delta for each asset across the entire batch, then checks
    /// whether any resulting position exceeds the limit.
    /// </summary>
    /// <param name="orders">Orders sharing one decision date; an empty batch is allowed and mixed dates are rejected.</param>
    /// <param name="portfolio">The current portfolio; risk NAV is independent of reporting equity.</param>
    /// <returns>The netted cap/tolerance decision, or a diagnostic rejection.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">Current NAV is nonpositive, or a required valuation input is unavailable.</exception>
    public RiskEvaluation EvaluateBatch(IReadOnlyList<Order> orders, IPortfolio portfolio)
    {
        Guard.AgainstNull(() => orders);
        Guard.AgainstNull(() => portfolio);

        if (orders.Count == 0)
        {
            return RiskEvaluation.Allowed;
        }
        var date = orders[0].Timestamp;
        if (orders.Any(order => order.Timestamp != date))
        {
            return RiskEvaluation.Rejected("Risk batch contains mixed decision dates; cannot select a causal valuation date.");
        }
        var totalPortfolioValue = RiskValuation.PositiveNav(portfolio, date);

        portfolio.HistoricalMarketData.TryGetValue(date, out var latestMarketData);
        if (latestMarketData is null)
        {
            return RiskEvaluation.Rejected($"No market data available on {date:yyyy-MM-dd}; cannot evaluate position size.");
        }

        // 1. Collect current positions across all strategies
        var currentPositions = new Dictionary<Symbol, int>();
        foreach (var strategy in portfolio.Strategies.Values)
        {
            foreach (var (asset, qty) in strategy.Positions)
            {
                currentPositions[asset] = currentPositions.GetValueOrDefault(asset) + qty;
            }
        }

        // 2. Compute net deltas from the entire batch
        var deltas = new Dictionary<Symbol, int>();
        foreach (var order in orders)
        {
            var delta = order.TradeAction == TradeAction.Buy
                ? order.Quantity
                : -order.Quantity;
            deltas[order.Symbol] = deltas.GetValueOrDefault(order.Symbol) + delta;
        }

        // 3. Check projected position for each affected asset
        foreach (var (asset, delta) in deltas)
        {
            if (!latestMarketData.TryGetValue(asset, out var marketData))
            {
                continue;
            }

            var projectedQty = currentPositions.GetValueOrDefault(asset) + delta;
            var result = CheckProjectedPosition(portfolio, date, asset, projectedQty, marketData.AdjustedClose, totalPortfolioValue, when: " after batch");
            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return RiskEvaluation.Allowed;
    }

    /// <summary>
    /// Compares a projected position's base-currency weight against the cap plus the share-granularity
    /// tolerance <c>max(Tolerance, one share's weight)</c>; see the class remarks.
    /// </summary>
    /// <param name="portfolio">The portfolio whose base currency, asset currencies and FX tables apply.</param>
    /// <param name="asset">The asset whose projected position is checked.</param>
    /// <param name="projectedQuantity">The asset's signed quantity after the order(s) under evaluation.</param>
    /// <param name="price">The asset's decision-date <c>AdjustedClose</c> in its own currency.</param>
    /// <param name="date">The order batch decision date used for marks and FX.</param>
    /// <param name="totalPortfolioValue">The current marked holdings and currency-cash NAV in base currency; positive.</param>
    /// <param name="when">Suffix naming the evaluation context in the rejection message (empty, or " after batch").</param>
    private RiskEvaluation CheckProjectedPosition(
        IPortfolio portfolio,
        DateOnly date,
        Symbol asset,
        int projectedQuantity,
        decimal price,
        decimal totalPortfolioValue,
        string when)
    {
        var projectedValue = PositionValuation.AbsoluteValueInBase(portfolio, date, asset, projectedQuantity, price);
        var positionPercent = projectedValue / totalPortfolioValue;
        var tolerance = Math.Max(Tolerance, PositionValuation.OneShareWeight(portfolio, date, asset, price, totalPortfolioValue));

        if (positionPercent > _maxPositionPercent + tolerance)
        {
            return RiskEvaluation.Rejected(
                $"Position in {asset} would be {positionPercent:P2} of portfolio{when}, exceeding maximum {_maxPositionPercent:P2} " +
                $"by more than the {tolerance:P4} tolerance (the larger of {Tolerance:P2} and one share of {asset} at the latest close).");
        }

        return RiskEvaluation.Allowed;
    }
}

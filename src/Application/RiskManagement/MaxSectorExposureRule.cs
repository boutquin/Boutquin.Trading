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
/// Rejects orders that would cause total exposure to any asset class to exceed a configured
/// percentage of total portfolio value, with each holding valued in the portfolio's base currency at
/// the decision-date bar. Symbol classes are determined by a user-provided mapping.
/// </summary>
/// <remarks>
/// <para>Risk NAV is calculated from current holdings and all currency cash at the decision date;
/// reporting equity and its burn-in window do not control admission. Nonpositive NAV throws
/// <see cref="InvalidOperationException"/>; unavailable valuation inputs propagate their existing errors.</para>
/// <para><b>Share-granularity tolerance:</b> the cap is enforced to within one share of each holding in
/// the class. Position sizing rounds every holding's share count with
/// <see cref="MidpointRounding.AwayFromZero"/>, so a class whose holdings sit exactly at their targets
/// overshoots its cap by up to half a share per holding. The rule therefore rejects only when the
/// projected exposure exceeds <c>cap + max(Tolerance, Σ w₁)</c>, where <c>Σ w₁</c> sums one share's
/// base-currency weight at the decision-date <c>AdjustedClose</c> over the class's projected non-zero holdings.
/// The fixed tolerance stays binding whenever those shares are together worth less than it, the
/// rounding overshoot always fits inside the bound, and anything more than one share per holding over
/// the cap is rejected. The sizers never round toward the cap.</para>
/// <para><b>Scope:</b> <see cref="Evaluate"/> checks only the class of the order's asset, with that
/// asset's quantity adjusted to its post-trade value; <see cref="EvaluateBatch"/> checks every class
/// after netting the whole batch. Both net an asset's quantity across strategies before valuing it, and
/// both skip a holding that has no bar on the decision date because it cannot be valued.</para>
/// </remarks>
public sealed class MaxSectorExposureRule : IRiskRule
{
    /// <summary>
    /// Floor of the exposure comparison tolerance (10 basis points = 0.10%). Sector exposure aggregates
    /// several positions, each with its own share-count rounding, so the floor is wider than the
    /// single-position rule's 1 bp. The tolerance actually applied is <c>max(Tolerance, Σ one share's
    /// weight)</c> over the class's holdings — see the class remarks.
    /// </summary>
    private const decimal Tolerance = 0.001m;

    private readonly decimal _maxExposurePercent;
    private readonly IReadOnlyDictionary<Symbol, AssetClassCode> _assetClassMap;

    /// <summary>
    /// Initializes a new instance of <see cref="MaxSectorExposureRule"/>.
    /// </summary>
    /// <param name="maxExposurePercent">
    /// The maximum allowable exposure per asset class as a fraction of portfolio value (e.g., 0.40 for 40%).
    /// </param>
    /// <param name="assetClassMap">
    /// A mapping from asset to its asset class code.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxExposurePercent"/> is not between 0 (exclusive) and 1 (inclusive).
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assetClassMap"/> is null.
    /// </exception>
    public MaxSectorExposureRule(
        decimal maxExposurePercent,
        IReadOnlyDictionary<Symbol, AssetClassCode> assetClassMap)
    {
        if (maxExposurePercent is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExposurePercent),
                maxExposurePercent,
                "Max exposure percent must be between 0 (exclusive) and 1 (inclusive).");
        }

        _maxExposurePercent = maxExposurePercent;
        _assetClassMap = assetClassMap ?? throw new ArgumentNullException(nameof(assetClassMap));
    }

    /// <inheritdoc />
    public string Name => "MaxSectorExposure";

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

        // Determine the asset class of the order's asset
        if (!_assetClassMap.TryGetValue(order.Symbol, out var orderAssetClass))
        {
            return RiskEvaluation.Rejected(
                $"Symbol '{order.Symbol}' has no asset class mapping; cannot evaluate sector exposure.");
        }

        portfolio.HistoricalMarketData.TryGetValue(date, out var latestMarketData);
        if (latestMarketData is null)
        {
            return RiskEvaluation.Rejected(
                "No market data available to evaluate sector exposure.");
        }

        // Project the order's asset to its post-trade quantity; every other holding stays as is.
        var projectedPositions = CollectCurrentPositions(portfolio);
        var delta = order.TradeAction == TradeAction.Buy
            ? order.Quantity
            : -order.Quantity;
        projectedPositions[order.Symbol] = projectedPositions.GetValueOrDefault(order.Symbol) + delta;

        return CheckProjectedExposures(
            projectedPositions, latestMarketData, portfolio, date, totalPortfolioValue, onlyAssetClass: orderAssetClass, when: string.Empty);
    }

    /// <summary>
    /// Evaluates the projected sector exposures after ALL orders in the batch.
    /// Computes the net position deltas per asset, then sums sector-level exposure
    /// to check whether any sector exceeds the limit.
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
            return RiskEvaluation.Rejected(
                "No market data available to evaluate sector exposure.");
        }

        // Verify all orders have asset class mappings
        foreach (var order in orders)
        {
            if (!_assetClassMap.ContainsKey(order.Symbol))
            {
                return RiskEvaluation.Rejected(
                    $"Symbol '{order.Symbol}' has no asset class mapping; cannot evaluate sector exposure.");
            }
        }

        // Net the whole batch onto the current positions
        var projectedPositions = CollectCurrentPositions(portfolio);
        foreach (var order in orders)
        {
            var delta = order.TradeAction == TradeAction.Buy
                ? order.Quantity
                : -order.Quantity;
            projectedPositions[order.Symbol] = projectedPositions.GetValueOrDefault(order.Symbol) + delta;
        }

        return CheckProjectedExposures(
            projectedPositions, latestMarketData, portfolio, date, totalPortfolioValue, onlyAssetClass: null, when: " after batch");
    }

    /// <summary>Sums each asset's quantity across all strategies.</summary>
    private static Dictionary<Symbol, int> CollectCurrentPositions(IPortfolio portfolio)
    {
        var positions = new Dictionary<Symbol, int>();
        foreach (var strategy in portfolio.Strategies.Values)
        {
            foreach (var (asset, qty) in strategy.Positions)
            {
                positions[asset] = positions.GetValueOrDefault(asset) + qty;
            }
        }

        return positions;
    }

    /// <summary>
    /// Sums each asset class's projected base-currency exposure and one-share granularity, then compares
    /// the exposure against the cap plus <c>max(Tolerance, Σ one share's weight)</c>; see the class remarks.
    /// </summary>
    /// <param name="projectedPositions">Each asset's signed quantity after the order(s) under evaluation, netted across strategies.</param>
    /// <param name="latestMarketData">The decision-date bar per asset; a holding without one is skipped.</param>
    /// <param name="portfolio">The portfolio whose base currency, asset currencies and FX tables apply.</param>
    /// <param name="date">The order batch decision date used for marks and FX.</param>
    /// <param name="totalPortfolioValue">The current marked holdings and currency-cash NAV in base currency; positive.</param>
    /// <param name="onlyAssetClass">When set, only this class is checked; otherwise every class is.</param>
    /// <param name="when">Suffix naming the evaluation context in the rejection message (empty, or " after batch").</param>
    private RiskEvaluation CheckProjectedExposures(
        IReadOnlyDictionary<Symbol, int> projectedPositions,
        SortedDictionary<Symbol, Bar> latestMarketData,
        IPortfolio portfolio,
        DateOnly date,
        decimal totalPortfolioValue,
        AssetClassCode? onlyAssetClass,
        string when)
    {
        var exposures = new Dictionary<AssetClassCode, decimal>();
        var granularities = new Dictionary<AssetClassCode, decimal>();

        foreach (var (asset, quantity) in projectedPositions)
        {
            if (quantity == 0)
            {
                continue;
            }

            if (!_assetClassMap.TryGetValue(asset, out var assetClass))
            {
                continue;
            }

            if (onlyAssetClass is { } only && assetClass != only)
            {
                continue;
            }

            if (!latestMarketData.TryGetValue(asset, out var bar))
            {
                continue;
            }

            exposures[assetClass] = exposures.GetValueOrDefault(assetClass)
                + PositionValuation.AbsoluteValueInBase(portfolio, date, asset, quantity, bar.AdjustedClose);
            granularities[assetClass] = granularities.GetValueOrDefault(assetClass)
                + PositionValuation.OneShareWeight(portfolio, date, asset, bar.AdjustedClose, totalPortfolioValue);
        }

        foreach (var (assetClass, exposure) in exposures)
        {
            var exposurePercent = exposure / totalPortfolioValue;
            var tolerance = Math.Max(Tolerance, granularities[assetClass]);

            if (exposurePercent > _maxExposurePercent + tolerance)
            {
                return RiskEvaluation.Rejected(
                    $"Exposure to {assetClass} would be {exposurePercent:P2}{when}, exceeding maximum {_maxExposurePercent:P2} " +
                    $"by more than the {tolerance:P4} tolerance (the larger of {Tolerance:P2} and one share of each holding in the class at the latest close).");
            }
        }

        return RiskEvaluation.Allowed;
    }
}

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

namespace Boutquin.Trading.Application.ExpectedReturns;

using Domain.ValueObjects;

/// <summary>
/// A forward-looking expected-return estimator implementing Ilmanen's building-block decomposition, so a
/// return-aware optimizer anchors on observable yields and macro inputs rather than extrapolating the
/// realized sample mean (Ilmanen, A. (2012), <i>Expected Returns on Major Asset Classes</i>, CFA Institute
/// Research Foundation).
/// </summary>
/// <remarks>
/// <para><b>Per-asset-class building blocks (Ilmanen 2012).</b> Expected return takes a different observable
/// form per asset class, so the estimator is constructed with a per-symbol
/// <see cref="ExpectedReturnAssetClass"/> map selecting the decomposition applied to each asset:</para>
/// <list type="bullet">
///   <item><description><b>Bond</b> — <c>E[r] ≈ current yield</c>: "the yield on a riskless bond was the
///   expected return of that bond" (Foreword, p. vi). The current yield is the latest observation of the
///   asset's own <b>per-symbol</b> auxiliary series (keyed by the asset's symbol). This is the plain
///   current-yield anchor — a documented V1 simplification of the full near-term rule
///   <c>BRP_H ≈ (Y10 − Y1) − Duration10 · E[ΔY10]</c> (eq. 3, p. 48); the rolldown/curve-shape term is a
///   known intended omission.</description></item>
///   <item><description><b>Equity / REIT</b> — <c>E[r] = income (dividend) yield + trend real growth +
///   inflation</c>, the Gordon-growth / DDM building blocks: "A feasible long-run return on equities is then
///   the sum of the cash flow yield (D/P) and the trend cash flow growth rate G" (§2.4.3, pp. 28–29),
///   formalized <c>ERP = D/P + G − Y</c> (eq. 1, p. 29), in nominal terms <c>E[r] = D/P + G_real +
///   inflation</c> (p. 29). The income yield <c>D/P</c> is the latest observation of the asset's own
///   <b>per-symbol</b> trailing income-yield series; <c>G_real</c> (trend real growth) and <c>inflation</c>
///   are the latest observations of two <b>market-wide</b> series.</description></item>
/// </list>
/// <para><b>Forward-looking, not rear-view (the byte-identity fidelity property):</b> no term reads the realized
/// <c>returns</c> series — the estimate is a pure function of the auxiliary yields/macro inputs. With those
/// held constant, the output is constant regardless of realized-price noise. Ilmanen (Introduction, pp. 4–5)
/// prefers such anchors precisely because they "have a better track record … than do rearview-mirror
/// measures", not collapsing to extrapolating the sample mean.</para>
/// <para><b>Aux-series delivery (constructor-bound + method override).</b> The construction-model call
/// path carries no auxiliary-series parameter, so the estimator MUST also accept its per-symbol series
/// <b>constructor-bound</b> (the <c>TacticalOverlayConstruction.currentRegime</c> precedent — the caller
/// constructs the estimator per run with the current universe's data), in one of two forms:
/// <b>positional</b> (this constructor's <c>auxiliarySeries</c> map, sound for single-evaluation
/// computation where the caller guarantees each array ends at the evaluation date) or
/// <b>date-indexed</b> (a <see cref="DatedAuxiliarySeries"/>, for multi-rebalance backtests — sliced to
/// the evaluation date at each date-aware call, so a latest-observation read can never see past the
/// decision point). The <c>auxiliarySeries</c> method parameter is an optional override for direct
/// callers: a non-null value overrides the constructor-bound series for that call; a null value falls
/// back to them. Without the constructor-bound forms the estimator would be unreachable from any
/// construction model.</para>
/// <para><b>Key space:</b> the <c>auxiliarySeries</c> keys are of two kinds — <b>per-asset</b> series keyed
/// by the scored asset's own <see cref="Symbol"/> (a bond's current-yield series, or an equity/REIT's
/// trailing income-yield series) and <b>market-wide</b> series (the trend-real-growth and breakeven-inflation
/// series identifiers, default <c>REALGROWTH</c> and <c>T10YIE</c>).</para>
/// <para><b>Missing-input contract (throws — never a silent hybrid).</b> Per the positional,
/// fully-populated <see cref="IExpectedReturnEstimator"/> contract an estimator cannot omit an asset, so
/// unlike the signal layer this estimator does <b>not</b> drop assets with missing inputs. It
/// <b>throws</b> <see cref="Boutquin.Trading.Domain.Exceptions.CalculationException"/> naming <b>every</b>
/// symbol that cannot be estimated — an asset absent from the class map, or lacking (or with an empty)
/// required per-asset series, or (for equity) whose required market-wide growth/inflation series is absent
/// or empty. It never falls back to the sample mean for a subset, which would be indistinguishable
/// downstream from a correct estimate.</para>
/// <para><b>Thread-safety:</b> stateless after construction (the bound maps are read-only) and therefore
/// safe for concurrent use.</para>
/// </remarks>
public sealed class YieldAnchoredExpectedReturnEstimator : IExpectedReturnEstimator
{
    private readonly IReadOnlyDictionary<Symbol, ExpectedReturnAssetClass> _assetClasses;
    private readonly IReadOnlyDictionary<Symbol, decimal[]>? _boundAuxiliarySeries;
    private readonly DatedAuxiliarySeries? _boundDatedAuxiliarySeries;
    private readonly Symbol _realGrowthKey;
    private readonly Symbol _inflationKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="YieldAnchoredExpectedReturnEstimator"/> class.
    /// </summary>
    /// <param name="assetClasses">
    /// Per-symbol map from each asset to its <see cref="ExpectedReturnAssetClass"/>, selecting the building-block
    /// decomposition applied to that asset. An asset absent from this map cannot be estimated and is named in
    /// the thrown <see cref="Boutquin.Trading.Domain.Exceptions.CalculationException"/>. Validated non-null.
    /// </param>
    /// <param name="auxiliarySeries">
    /// Optional constructor-bound per-symbol auxiliary series (the aux-series delivery channel for callers
    /// reaching the estimator through <c>IPortfolioConstructionModel.ComputeTargetWeights</c>, which has no
    /// auxiliary-series parameter). A non-null <c>auxiliarySeries</c> passed to
    /// <see cref="EstimateExpectedReturns(IReadOnlyList{Symbol}, decimal[][], IReadOnlyDictionary{Symbol, decimal[]})"/> overrides this bound map for that call. Defaults to
    /// <see langword="null"/> (no bound series — every call must then supply them via the method parameter).
    /// </param>
    /// <param name="realGrowthSeriesId">
    /// The market-wide <c>auxiliarySeries</c> key for the equity/REIT trend-real-growth term (default
    /// <c>REALGROWTH</c>). Must be non-empty.
    /// </param>
    /// <param name="inflationSeriesId">
    /// The market-wide <c>auxiliarySeries</c> key for the equity/REIT inflation term (default <c>T10YIE</c>,
    /// the FRED 10-year breakeven inflation rate). Must be non-empty.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assetClasses"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a series-id argument is <see langword="null"/> or empty.</exception>
    public YieldAnchoredExpectedReturnEstimator(
        IReadOnlyDictionary<Symbol, ExpectedReturnAssetClass> assetClasses,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null,
        string realGrowthSeriesId = "REALGROWTH",
        string inflationSeriesId = "T10YIE")
    {
        Guard.AgainstNull(() => assetClasses);
        Guard.AgainstNullOrWhiteSpace(() => realGrowthSeriesId);
        Guard.AgainstNullOrWhiteSpace(() => inflationSeriesId);

        _assetClasses = assetClasses;
        _boundAuxiliarySeries = auxiliarySeries;
        _realGrowthKey = new Symbol(realGrowthSeriesId);
        _inflationKey = new Symbol(inflationSeriesId);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YieldAnchoredExpectedReturnEstimator"/> class bound
    /// to <b>date-indexed</b> auxiliary series, so that one estimator instance can serve every rebalance
    /// of a backtest without lookahead: at each date-aware
    /// <see cref="EstimateExpectedReturns(IReadOnlyList{Symbol}, decimal[][], IReadOnlyDictionary{Symbol, decimal[]}, DateOnly)"/>
    /// call the dated series are sliced to the observations known at that rebalance's evaluation date
    /// before any yield/macro term is read.
    /// </summary>
    /// <param name="assetClasses">
    /// Per-symbol map from each asset to its <see cref="ExpectedReturnAssetClass"/>, exactly as on the
    /// positional-series constructor. Validated non-null.
    /// </param>
    /// <param name="datedAuxiliarySeries">
    /// Date-indexed per-symbol and market-wide auxiliary series covering the full backtest window. Per
    /// evaluation, <see cref="DatedAuxiliarySeries.SliceUpTo"/> reduces them to the positional view this
    /// estimator's latest-observation reads may know at the evaluation date. A symbol whose slice is
    /// empty at some date (no observation on or before it) is un-estimable at that date and surfaces
    /// through the standard <see cref="Boutquin.Trading.Domain.Exceptions.CalculationException"/>
    /// missing-input contract. Validated non-null (to bind positional series aligned by the caller, use
    /// the positional-series constructor).
    /// </param>
    /// <param name="realGrowthSeriesId">
    /// The market-wide key for the equity/REIT trend-real-growth term (default <c>REALGROWTH</c>). Must
    /// be non-empty.
    /// </param>
    /// <param name="inflationSeriesId">
    /// The market-wide key for the equity/REIT inflation term (default <c>T10YIE</c>). Must be
    /// non-empty.
    /// </param>
    /// <remarks>
    /// <para><b>Requires the date-aware entry point:</b> an instance constructed this way must be
    /// invoked through the date-aware overload (which the date-aware construction models call) — the
    /// date-free overload has no evaluation date to slice by and throws
    /// <see cref="InvalidOperationException"/> rather than silently reading the run-end tail.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assetClasses"/> or <paramref name="datedAuxiliarySeries"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when a series-id argument is <see langword="null"/> or empty.</exception>
    public YieldAnchoredExpectedReturnEstimator(
        IReadOnlyDictionary<Symbol, ExpectedReturnAssetClass> assetClasses,
        DatedAuxiliarySeries datedAuxiliarySeries,
        string realGrowthSeriesId = "REALGROWTH",
        string inflationSeriesId = "T10YIE")
    {
        Guard.AgainstNull(() => assetClasses);
        Guard.AgainstNull(() => datedAuxiliarySeries);
        Guard.AgainstNullOrWhiteSpace(() => realGrowthSeriesId);
        Guard.AgainstNullOrWhiteSpace(() => inflationSeriesId);

        _assetClasses = assetClasses;
        _boundDatedAuxiliarySeries = datedAuxiliarySeries;
        _realGrowthKey = new Symbol(realGrowthSeriesId);
        _inflationKey = new Symbol(inflationSeriesId);
    }

    /// <inheritdoc />
    // RS0027: mirrors the interface's suppression — the optional auxiliarySeries default predates
    // the date-aware overload and removing it would break existing two-argument call sites.
#pragma warning disable RS0027
    public decimal[] EstimateExpectedReturns(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries = null)
#pragma warning restore RS0027
    {
        if (_boundDatedAuxiliarySeries is not null)
        {
            throw new InvalidOperationException(
                "This YieldAnchoredExpectedReturnEstimator was constructed with date-indexed auxiliary " +
                "series, which must be sliced to an evaluation date before any yield/macro term is read. " +
                "Call the date-aware overload EstimateExpectedReturns(assets, returns, auxiliarySeries, " +
                "asOf) instead — the date-free overload has no date to slice by and will not silently " +
                "read the full (run-end) series tail.");
        }

        // The method parameter overrides the constructor-bound map for this call; a null parameter falls
        // back to the constructor-bound series (the aux-series delivery channel for callers reaching the
        // estimator through IPortfolioConstructionModel.ComputeTargetWeights, which has no aux parameter).
        return EstimateCore(assets, returns, auxiliarySeries ?? _boundAuxiliarySeries);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>Date-indexed instances:</b> the constructor-bound <see cref="DatedAuxiliarySeries"/> is
    /// sliced to observations dated on or before <paramref name="asOf"/> before any latest-observation
    /// read, so every yield/macro term is correctly dated for the evaluation point. A non-null
    /// <paramref name="auxiliarySeries"/> still overrides the bound series for this call (the caller
    /// then owns the alignment). Positional-series instances behave exactly as the date-free overload
    /// (there are no dates to slice by).</para>
    /// </remarks>
    public decimal[] EstimateExpectedReturns(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        DateOnly asOf)
    {
        var aux = auxiliarySeries
            ?? (_boundDatedAuxiliarySeries is not null
                ? _boundDatedAuxiliarySeries.SliceUpTo(asOf)
                : _boundAuxiliarySeries);

        return EstimateCore(assets, returns, aux);
    }

    private decimal[] EstimateCore(
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        IReadOnlyDictionary<Symbol, decimal[]>? aux)
    {
        Guard.AgainstNull(() => assets);

        var n = assets.Count;
        if (n == 0)
        {
            return [];
        }

        if (returns is null || returns.Length != n)
        {
            throw new ArgumentException("Returns array must have one series per asset.", nameof(returns));
        }

        var result = new decimal[n];

        // The positional, fully-populated contract forbids omitting an asset: collect EVERY symbol that
        // cannot be estimated and throw once, naming them all — never a silent per-asset fallback.
        List<string>? unestimable = null;

        for (var i = 0; i < n; i++)
        {
            var asset = assets[i];

            if (!_assetClasses.TryGetValue(asset, out var assetClass))
            {
                (unestimable ??= []).Add($"{asset} (no asset-class assignment)");
                continue;
            }

            switch (assetClass)
            {
                case ExpectedReturnAssetClass.Bond:
                    // Bond E[r] = current yield: latest observation of the asset's own per-symbol series.
                    if (TryLatest(aux, asset, out var yield))
                    {
                        result[i] = yield;
                    }
                    else
                    {
                        (unestimable ??= []).Add($"{asset} (missing current-yield series)");
                    }

                    break;

                case ExpectedReturnAssetClass.Equity:
                    // Equity/REIT E[r] = income yield (per-asset) + trend real growth + inflation (both
                    // market-wide). Any absent term makes the asset un-estimable.
                    var haveIncome = TryLatest(aux, asset, out var incomeYield);
                    var haveGrowth = TryLatest(aux, _realGrowthKey, out var realGrowth);
                    var haveInflation = TryLatest(aux, _inflationKey, out var inflation);

                    if (haveIncome && haveGrowth && haveInflation)
                    {
                        result[i] = incomeYield + realGrowth + inflation;
                    }
                    else
                    {
                        var missing = new List<string>(3);
                        if (!haveIncome)
                        {
                            missing.Add("income yield");
                        }

                        if (!haveGrowth)
                        {
                            missing.Add($"trend real growth '{_realGrowthKey}'");
                        }

                        if (!haveInflation)
                        {
                            missing.Add($"inflation '{_inflationKey}'");
                        }

                        (unestimable ??= []).Add($"{asset} (missing {string.Join(", ", missing)})");
                    }

                    break;

                default:
                    (unestimable ??= []).Add($"{asset} (unsupported asset class '{assetClass}')");
                    break;
            }
        }

        if (unestimable is not null)
        {
            throw new Boutquin.Trading.Domain.Exceptions.CalculationException(
                "YieldAnchoredExpectedReturnEstimator cannot estimate an expected return for the following " +
                $"asset(s) because a required input is absent or empty: {string.Join("; ", unestimable)}. " +
                "The estimator never falls back to the sample mean for a subset; supply the required " +
                "auxiliary series for every asset (a bond needs its per-symbol current-yield series; an " +
                "equity/REIT needs its per-symbol income-yield series plus the market-wide trend-real-growth " +
                "and inflation series).");
        }

        return result;
    }

    /// <summary>
    /// Reads the most recent (last) observation of the auxiliary series under <paramref name="key"/>.
    /// Returns <see langword="false"/> when the series is absent or empty (treated as an absent input).
    /// </summary>
    private static bool TryLatest(
        IReadOnlyDictionary<Symbol, decimal[]>? auxiliarySeries,
        Symbol key,
        out decimal latest)
    {
        latest = 0m;
        if (auxiliarySeries is not null
            && auxiliarySeries.TryGetValue(key, out var series)
            && series is not null
            && series.Length > 0)
        {
            latest = series[^1];
            return true;
        }

        return false;
    }
}

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.3.1] — 2026-11-02

The first stable 2.x release. The 2.3.0 entry below was prepared but never published; its changes ship here.

### Changed
- **Exact, signed dependencies from nuget.org alone:** Numerics 2.0.0, MarketData and MarketData.Adapter 2.0.1, Storage 2.0.1 and Domain 1.0.1 are pinned by exact version in `Directory.Packages.props`. `dependency-source.json` records each archive's hash, signature-excluded content hash, original payload and source revision, and `scripts/check-runner-dependencies.py` verifies them against a cold restore. NuGet configurations name only nuget.org, and `global.json` pins SDK 10.0.403.
- **Immutable release publishing:** packages are built once from the public source commit, frozen in a SHA-256 manifest, attached to the GitHub release and uploaded by `publish.yml` through nuget.org trusted publishing; no long-lived API key is stored. The earlier tag-push publisher and the sealed-publication workflows are removed.

### Fixed
- `SimulatedBrokerage` stop and stop-limit orders now evaluate their trigger and limit
  checks on the same price basis as the order. Order prices from
  `ClosePriceOrderPriceCalculationStrategy` are on the `Bar.AdjustedClose` basis, but the
  stop paths compared them with raw `High`/`Low`/`Close`, so on any dividend- or
  split-adjusted bar (`AdjustedClose != Close`) a stop could trigger when the adjusted range
  never reached it, or fail to trigger when it did. The bar's High/Low are now scaled by
  `AdjustedClose / Close` (factor 1 when `Close` is zero), and the stop-limit check uses
  `AdjustedClose`, matching the market and limit paths. Bars with `AdjustedClose == Close`
  behave exactly as before.

## [2.3.0] — not published; shipped as 2.3.1

### Fixed
- Correct `MaximumDiversificationConstruction` for feasible uniform asset bounds and
  symmetric positive-definite covariance. Bounds now constrain original asset weights;
  normalized-coordinate optimization followed by projection could return a suboptimal
  portfolio. Every supported success certifies exact emitted budget/bounds and true
  diversification-ratio relative loss at most 1e-10. Unresolved numerical results now
  throw `InvalidOperationException` instead of returning heuristic success. Existing
  empty/singleton and external-domain legacy behavior remains separately scoped.

### Security
- Require System.Security.Cryptography.Pkcs 9.0.21 in DataAccess to close the two October 2026 denial-of-service advisories inherited through SqlClient.

### Changed
- `RiskParityConstruction` now uses convex risk budgeting with decimal coordinate descent.
  Negative marginal risk at the starting weights is allowed. Binding bounds preserve the
  constrained risk-budget objective, which need not give equal risk to bound assets.
  Infeasible bounds and exhausted convergence now throw instead of relaxing limits or
  returning the last iterate. `maxIterations` limits each inner solve and the outer
  multiplier search (at most its square coordinate sweeps).
- Adopt the qualified Numerics 2.0 package. Covariance reference matrices follow the declared JMVA identity-target and fitted-noise detoning formulas; DCC tests retain feasible-objective and covariance identities, and standardized-moment assertions allow decimal rounding.

### Maintenance
- Update the .NET 10 framework dependencies to 10.0.12 and compatible test tooling.
  Restore the missing local Storage source so a cold restore resolves MarketData's
  transitive packages without relying on an existing global NuGet cache.
- Center the Python EWMA covariance test helper in `tests/Verification/test_covariance.py` on the
  EWMA-weighted mean, as the vector generator and `Boutquin.Numerics` already do. The Python suite
  had carried one failing test since the 2026-06-22 vector regeneration; no vector changes.

### Added
- **Explicit, costed FX trades fund assets held outside the funding currency.** A strategy funded in
  CAD can now buy and rebalance USD-denominated assets: `FxFundingPlanner` nets each signal's expected
  cash flows per currency and emits `FxOrderEvent`s (denominated in the currency bought) ahead of the
  asset orders under an `FxFundingPolicy` (`FundShortfall` by default; `FundShortfallAndRepatriateSurplus`
  sweeps idle foreign cash back to base; `None` disables). `SimulatedBrokerage` fills them at the bar's
  rate with an `IFxCostModel` (`SpreadFxCostModel`, default 0 bps) and raises `FxFillEvent`s;
  `FxFillEventHandler` moves the cash and clips a conversion to what the sell bucket holds. Consumers
  register `FxOrderEventHandler` and `FxFillEventHandler` beside the four asset handlers. `IBrokerage`
  and `IPortfolio` gain `SubmitFxOrderAsync`, `FxFillOccurred` and an FX-aware `ProcessPendingOrdersAsync`
  as default members, so external implementers keep compiling and refuse FX loudly.
- **`BackTest.RunAsync(…, maxFxRateStalenessDays)`** decides how old an FX print may be to stand in for a
  missing same-day print. The default `0` accepts no staleness: a bar without a same-day print for a
  dataset currency aborts with a message naming the currency, the bar, the last print, its age and the
  bound. A currency whose FX history starts after the first bar is never back-filled.
- **`FxConversion`** (`Boutquin.Trading.Domain.Helpers`) is the one place the engine reads its FX map, and
  documents its contract: keyed by non-base currency, base-currency units per 1 unit of the keyed
  currency. Every consumer (`StrategyBase`, `ConstructionModelStrategy`, both position sizers, the
  rebalancing handler) goes through it.
- **`BacktestDatasetSpec.FxLeadDays`** requests extra calendar days of FX history before `StartDate` for
  the foreign-currency pairs, so a run that allows carry-forward has a print to carry when the FX source
  skipped the first bar. Default `0`.
- **Tax-engine contracts now express specific-lot elections, disallowed-loss destinations, and US
  taxable-income bands.** `ICostBasisMethod` has an identification-aware default overload that
  validates selected quantities and fails closed on old-only implementers; `LotIdentification`
  identifies same-day lots by purchase date and cost per share. `LossHarvestingResult` adds the
  with-friendly `Disposition` property and a secondary constructor while retaining its original
  four-argument constructor and deconstruction shape, including JSON deserialization of existing
  four-field payloads. `ITaxJurisdiction` adds a default,
  income-aware overload keyed by the four-value `FilingStatus` enum; existing scalar-rate paths are
  unchanged and the new path throws until an implementation explicitly supports band selection.
- **The tax FX contract now targets the Bank of Canada daily-average series.** Its documentation
  records the discontinued noon-rate boundary at 2017-04-28, the three separately dated conversions
  required for a foreign-currency disposition, and implementation-owned non-business-day behavior.
- **Signal degeneracy is now structurally distinguishable.** `DegenerateCrossSectionException`
  derives from `CalculationException` and is raised by the low-volatility and value-momentum signals
  when a cross-sectional statistic is undefined because every scored asset has the same value.
- **As-of / date-indexed auxiliary-series channel for the signal-tilt path (additive).** The positional `auxiliarySeries` channel (`IReadOnlyDictionary<Symbol, decimal[]>`) carries no dates, so signals read it from the tail — correct only when the bound arrays end at the decision date. A consumer that bound one positional dictionary for a whole backtest therefore fed every historical rebalance the run-end tail: lookahead bias for the tail-reading signals (`TimeSeriesMomentumSignal`'s trailing risk-free window, `CarrySignal`'s most-recent yield observations). Three additive pieces close this structurally:
  - **`DatedAuxiliarySeries`** (Domain value object): wraps `IReadOnlyDictionary<Symbol, IReadOnlyList<(DateOnly Date, decimal Value)>>`, validates each series strictly ascending by date at construction (naming the offending key), and exposes `SliceUpTo(asOf)` producing the positional view a signal may know at the evaluation date (inclusive boundary; a key with no observation on or before the date is omitted).
  - **Date-aware `IPortfolioConstructionModel.ComputeTargetWeights(assets, returns, DateOnly asOf)`** (default interface method): defaults to forwarding to the two-argument overload, so every existing model is byte-identical; `ConstructionModelStrategy`'s rebalance loop now calls it with the rebalance timestamp — the same as-of discipline it already applied to the price-return window. All seven wrapping decorators (`SignalTiltConstruction`, `TacticalOverlayConstruction`, `VolatilityTargetingConstruction`, `TurnoverPenalizedConstruction`, `WeightConstrainedConstruction`, `RegimeWeightConstrainedConstruction`, `PcaConstrainedConstruction`) override it and forward the evaluation date to their inner model, so the date reaches a date-aware model anywhere in a composed chain.
  - **`SignalTiltConstruction` date-indexed binding** (new constructor accepting `DatedAuxiliarySeries`): one decorator instance bound once now serves every rebalance without lookahead — at each date-aware call the dated series are sliced to the observations known at that rebalance before any signal reads them. Fail-loud guard: a date-indexed instance invoked through the date-free overload throws `InvalidOperationException` rather than silently reading the unsliced (run-end) tail. Positional-series instances are unchanged (the overloads coincide; the caller owns alignment).

  The estimator seam has the same defect class and gets the mirror treatment (additive):
  - **Date-aware `IExpectedReturnEstimator.EstimateExpectedReturns(assets, returns, auxiliarySeries, DateOnly asOf)`** (default interface method forwarding to the date-free overload — byte-identical for `HistoricalMeanExpectedReturnEstimator` and every positional-bound estimator). The estimator-consuming construction models (`MeanVarianceConstruction`, `MeanDownsideRiskConstruction`, `RobustMeanVarianceConstruction`) override the date-aware `ComputeTargetWeights` to forward the evaluation date to the estimator.
  - **`YieldAnchoredExpectedReturnEstimator` date-indexed binding** (new constructor accepting `DatedAuxiliarySeries`): yield/macro latest-observation reads are sliced to the evaluation date per call; the date-free overload on a dated instance throws `InvalidOperationException` (same fail-loud rationale); an as-of date before a required series' first observation surfaces as the standard `CalculationException` naming the affected symbols.
  - **`DatedAuxiliarySeries.FromEconomicSeries`** factory accepting `SortedDictionary<DateOnly, decimal>` series maps (the shape backtest datasets deliver economic data in), so composition needs no date→positional flattening; Example 06 now composes its signal-tilt auxiliary series this way and runs the dated path end-to-end.

  Mock-based consumers note: a mocking proxy (e.g. Moq) implements every interface member itself, so the default-interface-method forwarding does not run — mocks of `IPortfolioConstructionModel` handed to the rebalance loop must set up the three-argument overload as well.
- **Covariance-estimator hyperparameters are now configurable through `BacktestOptions` (additive).** A new `BacktestOptions.CovarianceParameters` object (bound from the `Backtest:CovarianceParameters` configuration subsection) carries declarative, per-estimator hyperparameters, so every configurable covariance estimator can be tuned through `IConfiguration`/`IOptions<T>` without hand-constructing the estimator outside dependency injection: EWMA decay `lambda`; the Denoised Ledoit-Wolf shrinkage flag; Detoned `detoningAlpha`; DoublySparse eigenvector threshold; FactorGraphicalLasso and POET factor counts (and the POET threshold multiplier); Gerber threshold `c`; NERCOME split fraction; and the DCC-NL scalars `alpha`/`beta` plus the `estimateParameters` opt-out. Each hyperparameter is nullable and defers to the estimator's own constructor default when unset, so an existing configuration that sets only `CovarianceEstimator` is unchanged. In particular, **DCC-NL parameter estimation can now be disabled from configuration** — set `Backtest:CovarianceParameters:DccNlEstimateParameters` to `false` to hold `(alpha, beta)` fixed (the cheaper, deterministic V1 behavior) and optionally set `DccNlAlpha`/`DccNlBeta`. An out-of-range value is rejected by the estimator's own constructor validation at DI-resolution time rather than being silently dropped. The covariance estimator is now also resolvable on its own as `ICovarianceEstimator` from the service provider.

### Fixed
- **Exposure limits now use current portfolio NAV during burn-in (behavior change).**
  `MaxPositionSizeRule` and `MaxSectorExposureRule` value marked holdings and every currency
  cash bucket at the order date through `IPortfolio.CalculateTotalPortfolioValue(timestamp)`,
  independently of empty or stale `EquityCurve` reporting rows. Single-order and batch checks
  retain their caps, share-granularity tolerances, and asset-class mappings. Mixed-date batches
  reject; nonpositive NAV throws and missing required valuation data propagates an error.
  Custom portfolio implementations and test doubles must supply current marked NAV through
  that method; a reporting-equity row alone no longer supplies the risk denominator.
- **Daily drawdown control tracks causal peaks before reporting begins (behavior change).**
  `DrawdownCircuitBreaker` uses the same current marked NAV during burn-in, so a pre-reporting
  peak can trigger liquidation during burn-in or later in the run. Reporting start dates and
  return sample windows remain unchanged, as do thresholds, next-bar liquidation, and cooldown
  rules. Saved paths can differ where the earlier risk history changes a rejection or trigger;
  unchanged reporting dates do not imply identical holdings or returns.
- `DrawdownCircuitBreaker` now aborts on refused/interrupted liquidation submissions or holdings
  remaining at the next session after liquidation was requested. A failed instance cannot replay
  possibly accepted closing orders. Missing fill bars remain visible failures, with no invented fills.
- `BackTest` evaluates daily drawdown control before allocation signals and suppresses those
  signals throughout cash cooldown, preventing competing liquidation/rebalance orders and early
  re-entry. Prices continue advancing; liquidation still fills at the next bar. With daily control,
  expenses are charged once before risk evaluation and target sizing, which can affect integer
  share rounding when fees are nonzero.
- **A target exactly at a position or sector cap no longer trips the cap when share rounding lands half a
  share over.** Position sizing rounds share counts half-up (`MidpointRounding.AwayFromZero`), so a weight set
  at the cap overshoots it by up to half a share on about half of all rebalances. With the fixed 1 bp tolerance
  one 100-unit share of a 100,000 portfolio (0.1 %) was enough to reject the order — and, under a
  batch-evaluating handler, the whole rebalance — leaving the portfolio uninvested instead of half a share over.
  `MaxPositionSizeRule` now tolerates `max(1 bp, one share's base-currency weight)` above the cap and
  `MaxSectorExposureRule` `max(10 bp, Σ one share per holding in the class)`; more than one share over the cap
  is still rejected, portfolios where a share is worth less than the fixed tolerance behave exactly as before,
  and share rounding itself is unchanged (the `backtest_*` golden vectors are byte-identical). The rejection
  message now states the tolerance applied.
- **FX rates were fetched the wrong way round.** `BacktestDatasetBuilder` requested `FxPair(base, foreign)`
  and stored the answer under the foreign currency, where every consumer multiplies a foreign amount by
  it to reach base. For a CAD-base run over a USD asset the stored rate was USD-per-CAD (0.77) instead of
  CAD-per-USD (1.30): a USD holding was valued at 59 % of its worth and its FX contribution to the equity
  curve was inverted. The builder now requests `FxPair(foreign, base)` — the orientation the Bank of
  Canada (`FXUSDCAD`) and Frankfurter adapters serve natively.
- **A missing FX print no longer surfaces as `Conversion rate not found` deep inside valuation.**
  `RunAsync` resolves each bar's FX table before pending orders are processed (see
  `maxFxRateStalenessDays` above).
- **Construction models optimized on native-currency returns and risk rules valued foreign positions in
  native currency.** `ConstructionModelStrategy` now converts each price into the base currency at that
  bar's rate before differencing, so a CAD portfolio's covariances, volatility target and signals carry
  the USD/CAD move a CAD investor bears (single-currency portfolios are unchanged). `MaxPositionSizeRule`
  and `MaxSectorExposureRule` value `quantity × price` in base currency before dividing by the
  base-currency equity curve; a USD position in a CAD portfolio was understated by the rate.
- **Position sizing divided a base-currency target value by a native-currency price.**
  `FixedWeightPositionSizer`, `DynamicWeightPositionSizer` and `RebalancingSignalEventHandler` now
  convert into the asset's currency (or price the asset in base) first. A 50 % CAD target in a 100 USD
  share at 1.30 CAD/USD bought 650 shares where 500 is right — a 62.5 % weight — hidden on 100 % buys only
  because the cash limiter clipped the oversized order.
- **A single degenerate asset no longer aborts the entire signal calculation.** Time-series momentum
  now omits an up-trending asset with zero realized volatility, and carry omits FX-hedged assets when
  the local short rate makes `1 + r_local` non-positive. Each signal emits one aggregated Warning per
  call naming the affected assets while preserving scores for the rest of the cross-section.
- **`CLAUDE.md` developer documentation de-staled after the data-layer extraction.** The project map and architecture notes still described the seven removed `Boutquin.Trading.Data.*` fetcher projects (Tiingo, Frankfurter, Fred, FamaFrench, TwelveData, CSV, Processor), their fetcher classes and removed interfaces, and an obsolete "required test coverage" list whose entries were all either already tested or deleted along with their source projects. The stale sections are removed or replaced with pointers to the `Boutquin.MarketData` ecosystem that now owns ingestion; the project and interface tables are regenerated from the live tree; and explicit test-ownership (Trading ↔ Numerics) and test-coverage policies are documented to prevent recurrence. Documentation-only — no code or package-content change.
- **`DccNlCovarianceEstimator` standardized residuals now use the GARCH model's demeaned innovations (breaking-behavioral).** The DCC devolatilization step previously divided the **raw** return by the GARCH innovation volatility (`r_t / √h_t`). Because `Boutquin.Numerics` `GarchOneOneEstimator` fits an **AR(1)-mean + GARCH(1,1)** model, the conditional variances and the exposed `StandardizedResiduals` are defined on the demeaned innovation `ε_t = r_t − μ − φ·r_{t−1}`; using the raw return left the conditional mean in the standardized residuals, biasing the NL-shrunk correlation target C̃, the QMLE objective, the estimated `(α, β)`, and every conditional covariance `H_t` for any series with non-zero mean or autocorrelation. The estimator now consumes `GarchFitResult.StandardizedResiduals` directly. DCC-NL covariance/forecast outputs change for all callers (both the estimating and `estimateParameters: false` paths); saved DCC-NL allocations must be regenerated. A non-converged GARCH fit is now surfaced as `CalculationException` rather than silently devolatilizing with unreliable variances.

### Changed
- **Same-bar fills run asset sells → FX conversions → asset buys (breaking-behavioral for cash-limited
  same-bar rebalances).** `SimulatedBrokerage.ProcessPendingOrdersAsync` previously filled in submission
  order, so a buy emitted before its funding sell could be cash-clipped although the sale filled a moment
  later on the same bar. The Python reference engines adopt the same ordering; ten of the eleven
  `backtest_*` golden vectors are byte-identical and `backtest_equal_weight_construction` plus the four
  `integration_*` vectors are regenerated (MinVar final equity now agrees between the engines to four
  decimals: 108,721.2754). The regenerated integration vectors also absorb ≈1e-8 optimizer drift from the
  current pypfopt/cvxpy toolchain, inside the 1e-6 test tolerance.
- **`SignalEventHandler` and `RebalancingSignalEventHandler` take an optional `FxFundingPolicy`**
  (default `FundShortfall`); `SimulatedBrokerage` takes an optional `IFxCostModel`. Source-compatible;
  the three constructor signatures changed in `PublicAPI.Unshipped.txt`.
- **`DccNlCovarianceEstimator` now estimates the DCC scalars `(α, β)` by default (breaking-behavioral).** `Fit()` estimates `(α, β)` from the return window by full two-step quasi-maximum-likelihood (Engle 2002, eq. 10), minimising the negative quasi log-likelihood via Nelder-Mead in a softmax reparameterization that keeps `α>0, β>0, α+β<1`. The constructor `alpha`/`beta` parameters (defaults `0.01`/`0.97`) are now the optimizer's **initial guess** rather than fixed values, so the conditional-covariance outputs change for any caller using the estimating default. The constructor gains an optional `estimateParameters` parameter (default `true`); pass **`new DccNlCovarianceEstimator(estimateParameters: false)`** to hold `(α, β)` fixed and reproduce the prior fixed-parameter outputs exactly. The full QMLE is used in place of the DCC-NL reference's composite-likelihood (2-MSCLE) estimator because the target universe is small (`N ≤ 10`), where the full N×N likelihood is tractable and more efficient; PSD of every `H_t` under `α+β<1` is unchanged. Saved DCC-NL allocations or tearsheets produced with the previous fixed `(0.01, 0.97)` defaults must be regenerated, or the call sites updated to opt out.

### Previously recorded development snapshot: 2.0.0-local-20260622

### Added
- **`Money` boundary APIs on the portfolio/strategy/fill reporting + construction surfaces (additive).** New currency-tagged `Boutquin.Domain.Money` companions sit alongside the existing `decimal` members, so a monetary amount can be aggregated and reported with compile-time currency safety while the hot numeric paths stay `decimal`:
  - Total value (reporting): `IPortfolio.CalculateTotalPortfolioValueAsMoney(timestamp)` and `IStrategy.ComputeTotalValueAsMoney(...)` return the existing `decimal` total tagged with the base currency.
  - Equity-curve & cash (reporting projections): `IPortfolio.EquityCurveAsMoney` and `IStrategy.CashAsMoney` are derived views over the existing `decimal`-keyed collections — each equity value tagged with the base currency, each cash bucket tagged with its own currency. The backing `EquityCurve`/`Cash` collections stay `decimal`.
  - Cash mutation (construction): `IPortfolio.UpdateCash(strategyName, Money)`, `IStrategy.UpdateCash(Money)`, and `IPortfolio.UpdateCashForDividend(asset, Money dividendPerShare, Money currentPrice)` overloads validate the supplied currency against the target bucket and route to the `decimal` core; a wrong-currency `Money` throws `Boutquin.Domain.Exceptions.CurrencyMismatchException` rather than silently crediting the wrong bucket.
  - Fill/commission (reporting): `FillEvent.FillPriceAsMoney(currency)` and `FillEvent.CommissionAsMoney(currency)` project the `decimal`-backed fill fields as currency-tagged `Money`; the `FillEvent` fields themselves stay `decimal`.

  All of the above are non-breaking — added as C# default interface methods (or instance methods on `FillEvent`), so existing implementers compile unchanged. The per-bar backtest loop, FX-rate stores, weights, ratios, and bps are untouched (proven byte-identical by the hot-path parity characterization).
- **`Money` adoption on the TaxEngine domain records.** The seven tax domain records in `Domain/TaxEngine/` now carry currency-tagged `Boutquin.Domain.Money` on their amount fields — `LotDisposal` (`CostPerShare`, `SalePrice`, `Commission`, `RealizedGainLoss`), `TaxLot` (`CostPerShare`, `Commission`), `DividendRecord` (`AmountPerShare`), `TaxImpact` (`GrossGainLoss`, `TaxableAmount`, `EstimatedTax`, `ShortTermGainLoss`, `LongTermGainLoss`), `DividendTaxImpact` (`GrossIncome`, `TaxableIncome`, `TaxCredits`, `EstimatedTax`), `LossHarvestingResult` (`DisallowedAmount`, `AllowedAmount`), and `RocAdjustmentResult` (`NewCostBasis`, `DeemedCapitalGain`). Cost basis, proceeds, commission, withholding, realized gain/loss, and loss-disallowance amounts are genuinely multi-currency; summing them across currencies now throws `Boutquin.Domain.Exceptions.CurrencyMismatchException` (via `Money` arithmetic) where the pre-`Money` `decimal` code computed silently. Dimensionless rate fields (e.g. `WithholdingTaxRate`) stay `decimal`.
- `TurnoverPenalizedConstruction` now exposes a stateless `ComputeTargetWeights(assets, returns, currentWeights)` overload that penalizes turnover against an explicitly-supplied previous-weight vector without reading or mutating the decorator's internally tracked state. This lets a single instance compute as-of penalized weights for many independent series (e.g. per-cohort quarterly rebalancing) from one call each, with no cross-contamination and no backtest loop. The existing two-argument overload is unchanged and now delegates to the shared core.

### Changed
- **`HierarchicalEqualRiskContribution` (HERC) now implements the true four-step Raffinot (2018) method (breaking-behavioral).** The previous implementation was an inverse-risk HRP variant: it reused HRP's single-linkage clustering but split each branch at the number-of-assets midpoint and allocated between branches by inverse standard deviation (1/σ). It is replaced by a faithful implementation of Raffinot (2018), *The Hierarchical Equal Risk Contribution Portfolio*: (1) single-linkage hierarchical clustering; (2) selection of the optimal number of clusters via the Gap index (Tibshirani et al. 2001); (3) top-down recursive division into two parts based on the **dendrogram shape** (not the number-of-assets midpoint) with an **equal-risk-contribution** allocation between the two cluster groups (`a₁ = RC₂/(RC₁+RC₂)`); (4) naive risk parity (inverse-variance) within each cluster. The public type, constructor, and `IPortfolioConstructionModel` signature are unchanged, but the computed weights differ materially from the previous version — **existing HERC backtest results are not reproducible across this change**. Any saved allocations, tearsheets, or comparisons that used HERC must be regenerated.
- **TaxEngine domain records adopt `Money` on their amount fields (breaking).** The amount fields of the seven tax records in `Domain/TaxEngine/` change type from `decimal` to `Boutquin.Domain.Money` (the full field list is in **Added** above). This is a record-shape break: constructing or deconstructing these records now uses `Money` for the amount positions. Rate fields stay `decimal`. Part of the major bump to `2.0.0`.
- **`DividendRecord` standalone `Currency` field removed (breaking).** After `AmountPerShare` became `Money`, the dividend's currency lives solely inside `AmountPerShare.Currency`. The former separately-stored `Currency` field is removed; `DividendRecord.Currency` is now a derived `=> AmountPerShare.Currency` accessor, never an independently stored value — a single dividend cannot carry two divergent currencies. Callers that constructed `DividendRecord` with a separate currency argument must drop it (the currency is carried by the `Money` amount).
- `IPortfolio.UpdateCashForDividend(Symbol, decimal, decimal)` no longer declares its `currentPrice` parameter optional on the interface (callers pass it explicitly), making room for the additive `Money` overload without an optional-parameter overload collision. The concrete `Portfolio.UpdateCashForDividend` retains its `currentPrice = 0` default, so existing two-argument calls on a `Portfolio` reference are unaffected.
- **Currency enum unified onto `Boutquin.Domain.Currency` (breaking).** Every use of the former `CurrencyCode` enum is replaced by `Boutquin.Domain.Currency` (the full ISO 4217 enumeration, 177 members, with an `Unspecified = 0` sentinel): `IPortfolio.BaseCurrency`/`AssetCurrencies`/`GetAssetCurrency`/`UpdateCash`/`HistoricalFxConversionRates`, every strategy's `Cash` dictionary key (`SortedDictionary<Currency, decimal>`), `IStrategy.Assets`/`ComputeTotalValue`/`GenerateSignals`, the position sizers, `MarketEvent`, `DividendRecord`, the currency-conversion/tax-FX interfaces, the recipe/backtest/example entry points, and the EF entities `Currency`/`FxRate`/`Country`. Both enumerations share identical ISO 4217 numeric backing (USD = 840, EUR = 978, …) and the former 13-member set is a same-name, same-value subset of `Currency`, so the swap preserves every serialized value: the EF currency columns (stored as the ISO alphabetic code via `HasConversion<string>()`) round-trip byte-identically, with no schema change or migration. This is a pure type unification — the `Money` adoption above is the separate boundary change.
- **`Unspecified = 0` default sentinel.** `default(Currency)` is now the defined `Currency.Unspecified` value (the former enum had no zero member). Undefined-value guards continue to throw for genuinely out-of-range values; a value such as the ISO "XXX" code (999) is now a defined member of the unified enum.
- **Upstream package pins.** `Boutquin.Domain` → `0.7.1-local-20260622`; `Boutquin.MarketData.*` / `Boutquin.MarketData.Adapter.*` → `1.1.0-local-20260622`.
- `MaxPositionSizeRule` now rejects orders when the referenced asset's current market price is unavailable, matching the safe-default behavior already used by `MaxSectorExposureRule`. Silently allowing orders under data uncertainty was an inconsistency that could let oversized positions through during data outages.
- `DrawdownCircuitBreaker` liquidation now covers short positions via buy-to-cover orders in addition to closing long positions. The previous path ignored shorts, leaving net-short portfolios partially liquidated when the breaker tripped.

### Fixed
- `SimulatedBrokerage` market orders now fill at adjusted open (`Open × (AdjustedClose / Close)`) instead of raw `Open`, ensuring consistency with position sizing and equity valuation, both of which use `AdjustedClose`. For dividend-paying ETFs where the raw/adjusted price ratio exceeds 4×, the previous raw-fill caused severe position underallocation and compounding portfolio distortion across rebalances.
- `SimulatedBrokerage` limit order fill conditions now compare against adjusted low (`Low × adjFactor`) and adjusted high (`High × adjFactor`) so that limit prices expressed in adjusted terms trigger correctly regardless of cumulative dividend history.
- `SimulatedBrokerage` now serializes access to `_pendingOrders` under a dedicated lock, eliminating the concurrent-mutation hazard that existed when `FillOccurred` handlers ran on pooled threads while new signals enqueued orders on the event-loop thread.
- `Position.Buy` now rejects `shares <= 0` (previously allowed zero-share buys, which produced no-op fills with a live `BookValue` entry).
- `ExchangeSchedule` now rejects `24:00:00` as an open/close time; the upper bound is exclusive so legal times are `[00:00:00, 24:00:00)`.
- `ReturnTiltedHrpConstruction` cluster-return softmax now subtracts the cluster-max before exponentiation, eliminating overflow for high-magnitude return inputs without changing the normalized result.
- `CorrelationAnalyzer.Analyze` now validates that supplied portfolio weights sum to 1.0 (within 1e-6). The diversification ratio is undefined for non-unit weight vectors, so silently accepting them produced misleading output.
- `PrincipalPortfolioAnalyzer` zero-sum detection threshold tightened from `1e-20m` (below `decimal` precision) to `1e-12m`, so genuinely tiny weight sums are flagged rather than silently passed through.
- `BrinsonFachlerAttributor` error messages now identify which input dictionary is missing a sector key, instead of citing the `assetNames` parameter unconditionally.
- `Examples/Program.cs ComputeDailyReturns` now throws `CalculationException` on zero prior-period equity instead of returning `0`, matching the `EquityCurveExtensions` convention.
- `DecimalArrayExtensions.CompoundAnnualGrowthRate` no longer contains a dead `totalYears == 0` guard (the upstream `tradingDaysPerYear > 0` guard already rules this out).
- `VolumeShareSlippage` XML docs now accurately describe the implementation: the volume cap bounds price impact, not fill quantity.
- `MomentumScore` XML summary disambiguates the two time windows ("lookback" vs "skip", both in months).
- `DrawdownPeriod` XML docs corrected from "calendar days" to "trading days", matching the `EquityCurveExtensions` convention.
- `TearSheet` XML docs corrected from "expressed as a percentage" to "raw decimal ratio", matching the repo-wide raw-decimal convention for metrics.
- Typos fixed: `SecurityId` ("See alse" → "See also"), `SignalType` (doubled `/// ///` on summary).

### Removed
- `BacktestOptions.EnableDividendReinvestment`, `BacktestOptions.AnnualExpenseRatioBps`, `BacktestOptions.AssetExpenseRatiosBps`, and `CostModelOptions.DefaultSpreadPercent`: these properties were defined on the options types but never consumed by `AddBoutquinTrading` or any factory, making them silent traps that looked configurable but had no effect. `Portfolio.EnableDividendReinvestment` (the actual toggle) remains unchanged. Apply per-backtest expense-ratio and DRIP settings directly on the `Portfolio` instance or via `BackTest` constructor parameters.

### Previously recorded development milestone: 2.0.0 (2026-04-17)

### Added
- `Boutquin.Trading.Recipes` project: `IBacktestDataset`, `BacktestDataset`, `BacktestDatasetBuilder`, `BacktestDatasetSpec`, and `FakeBacktestDataset` — provides the bridge between the backtest engine and the `Boutquin.MarketData` `IDataPipeline` for production data workflows.
- Nine new covariance estimators completing the four-tier lineup (13 total):
  - Linear shrinkage: `LedoitWolfConstantCorrelationEstimator` (average-correlation target — recommended default for equities), `LedoitWolfSingleFactorEstimator` (market factor target), `OracleApproximatingShrinkageEstimator` (OAS, Chen et al. 2010)
  - Nonlinear/denoising: `QuadraticInverseShrinkageEstimator` (per-eigenvalue shrinkage, gold standard for N ≥ 10), `TracyWidomDenoisedCovarianceEstimator` (sharper finite-sample threshold, preferred when T/N < 5)
  - Factor/sparse/nonparametric: `PoetCovarianceEstimator` (low-rank + sparse residual), `NercomeCovarianceEstimator` (nonparametric split-sample), `DoublySparseEstimator` (sparsifies eigenvectors + noise eigenvalues), `DetonedCovarianceEstimator` (PC1 market-factor shrinkage, Lopez de Prado 2020)

### Changed
- `CorrelationAnalyzer.Analyze` now delegates covariance computation to `Boutquin.Numerics.Statistics.SampleCovarianceEstimator`, removing the inline two-pass covariance loop (~25 lines). `RollingCorrelation` now delegates entirely to `Boutquin.Numerics.Statistics.PearsonCorrelation.Rolling`, removing ~30 lines of inline rolling-window arithmetic.
- `MonteCarloSimulator.Run` now delegates to `Boutquin.Numerics.MonteCarlo.BootstrapMonteCarloEngine`, removing the inline bootstrap loop, per-simulation mean/std computation, and private `Percentile` helper (~30 lines).
- `SampleCovarianceEstimator`, `ExponentiallyWeightedCovarianceEstimator`, `LedoitWolfShrinkageEstimator`, `DenoisedCovarianceEstimator`, and `DetonedCovarianceEstimator` now delegate to `Boutquin.Numerics.Statistics`, removing ~900 lines of duplicated math.
- `PrincipalPortfolioAnalyzer`, `EffectiveNumberOfBets`, and `PcaRegimeSignal` now use `Boutquin.Numerics.LinearAlgebra.JacobiEigenDecomposition`, removing ~400 lines of inline Jacobi sweeps.
- `FactorRegressor` now uses `Boutquin.Numerics.Solvers.OrdinaryLeastSquares<decimal>` (Householder QR, 28-digit accuracy) instead of hand-rolled Gaussian elimination in `double`.
- `DecimalArrayExtensions.ValueAtRisk` now delegates to `Boutquin.Numerics.Distributions.InverseNormal.Evaluate`, removing the inline 52-line Abramowitz & Stegun rational approximation.
- `MinimumVarianceConstruction` and `MeanVarianceConstruction` now use `Boutquin.Numerics.Solvers.ActiveSetQpSolver` (Cholesky active-set with correct KKT cross-covariance handling) instead of the hand-rolled `CholeskyQpSolver`, which had a bug: it ignored the `Σ_FC * w_C` cross-covariance adjustment when solving the free-variable sub-problem after fixing weights at bounds, causing constrained solutions to be suboptimal.
- `MaximumDiversificationConstruction` likewise migrated to `ActiveSetQpSolver`.
- `DecimalArrayExtensions.Skewness` and `Kurtosis` now delegate to `Boutquin.Numerics.Statistics.SampleSkewness.Compute` and `SampleExcessKurtosis.Compute`, removing ~40 lines of duplicated math.
- `RollingWindow<T>` is now consumed from `Boutquin.Numerics.Collections` instead of the local `Domain/Helpers` copy.
- `Boutquin.Numerics` version bumped to `1.1.0`.

### Fixed
- Collinear factor inputs to `FactorRegressor` now correctly throw `CalculationException` instead of `OverflowException`. The fix is in `Boutquin.Numerics.LinearAlgebra.Internal.HouseholderQr<T>.BuildXtXInverse`, which now converts decimal arithmetic overflow to `InvalidOperationException`.
- `MinimumVarianceConstruction` constrained QP now finds the true global minimum. The old `CholeskyQpSolver` ignored the KKT cross-covariance term `Σ_FC * w_C` when fixed-bound variables were present, causing the free-variable sub-problem to be solved against an incorrect RHS and producing a higher-variance portfolio than necessary. `ActiveSetQpSolver` accounts for the full KKT system and matches scipy SLSQP to float64 precision.

### Removed
- `Asset` value object removed from `Boutquin.Trading.Domain.ValueObjects`; replaced by `Boutquin.MarketData.Abstractions.ReferenceData.Symbol`. Both are `readonly record struct` with a `Ticker` property; `Symbol` is the canonical type at the data-access layer.
- `AssetClassCode`, `ContinentCode`, `CountryCode`, `CurrencyCode`, `DividendType`, `ExchangeCode`, `SecuritySymbolStandard`, and `TimeZoneCode` enums removed from `Boutquin.Trading.Domain.Enums`. All eight are now consumed from `Boutquin.MarketData.Abstractions.ReferenceData`, which is the canonical source. `Boutquin.Trading.Domain.Enums` retains only backtest-specific enums (`TradeAction`, `OrderType`, `SignalType`, `EconomicRegime`, `RebalancingFrequency`, `AccountType`, `HoldingPeriod`, `CanadianProvince`, `UsState`).
- `FamaFrenchDataset` enum removed; the `IDataPipeline` architecture uses `Boutquin.MarketData.Abstractions.ReferenceData.FactorDatasetId` (a string-typed record struct) for all factor dataset identification.
- `MarketDataNotFoundException`, `MarketDataProcessingException`, `MarketDataRetrievalException`, `MarketDataStorageException` removed from `Boutquin.Trading.Domain.Exceptions`; canonical versions live in `Boutquin.MarketData.Abstractions.Exceptions`. Also removed dead-code exceptions `SymbolReaderException`, `NegativeBusinessDaysPerYearException`, and `NegativeRiskFreeRateException` (zero usages).
- `Domain.Data.MarketData` and `Domain.Data.FxRateData` records removed; backtest infrastructure uses `Boutquin.MarketData.Abstractions.Records.Bar` and `FxRate` directly.
- `Boutquin.Trading.Domain.Helpers.CholeskyQpSolver` removed; replaced by `Boutquin.Numerics.Solvers.ActiveSetQpSolver`.
- `Boutquin.Trading.Domain.Helpers.RollingWindow<T>` removed; replaced by `Boutquin.Numerics.Collections.RollingWindow<T>`.

## [1.0.0] - 2026-03-30

First production release of the Boutquin.Trading quantitative trading framework for long-only ETF and equity backtesting.

### Core Engine
- Event-driven backtesting pipeline: MarketEvent → SignalEvent → OrderEvent → FillEvent
- Next-bar Open fills with quantity-limiting (no look-ahead bias)
- Multi-currency cash management and position tracking
- Burn-in period support for indicator warm-up
- Trading calendar integration with configurable composition modes
- Dividend reinvestment (DRIP) — optional whole-share reinvestment at Close price
- Expense ratio deduction — portfolio-level default + per-asset overrides in basis points

### Portfolio Construction (18 Models + 1 Decorator)
- EqualWeight, InverseVolatility, MinimumVariance, MeanVariance, RiskParity
- MaximumDiversification (Chopin & Briand 2008)
- HierarchicalRiskParity (Lopez de Prado 2016), HERC, ReturnTiltedHRP (Lohre et al. 2020)
- BlackLitterman, DynamicBlackLitterman
- MeanDownsideRisk with pluggable CVaR and DownsideDeviation measures
- RobustMeanVariance (minimax across covariance scenarios)
- TacticalOverlay, VolatilityTargeting, WeightConstrained, RegimeWeightConstrained
- TurnoverPenalized decorator with L1 penalty

### Covariance Estimation (4)
- Sample (N-1), EWMA, Ledoit-Wolf Shrinkage (with rho correction), Denoised (RMT)

### Downside Risk Measures (3)
- CVaR (Rockafellar-Uryasev 2000), DownsideDeviation, CDaR

### Analytics & Reporting
- Brinson-Fachler attribution, multi-factor OLS regression, correlation analysis
- Drawdown analysis, walk-forward optimization, Monte Carlo simulation
- Effective Number of Bets (Meucci 2009)
- HTML tearsheet with SVG charts, benchmark comparison report

### Risk Management
- Composite risk manager with MaxDrawdown, MaxPositionSize, MaxSectorExposure rules
- DrawdownCircuitBreaker for dynamic intervention

### Data Providers
- Tiingo, TwelveData (equities), Frankfurter (FX), FRED (economic), Fama-French (factors), CSV

### Infrastructure
- L1 memory cache + L2 CSV write-through (6 decorators)
- Full DI registration with explicit factory switches
- CancellationToken on all async APIs, structured logging
- 39 domain interfaces, 17 enums, tax engine extension points (6 interfaces, 9 domain records)
- 1,456 tests (1,452 unit + 4 architecture), cross-language verification against Python (81 golden vectors)
- .NET 10 / C# 14, TreatWarningsAsErrors, SourceLink, MinVer, Apache 2.0

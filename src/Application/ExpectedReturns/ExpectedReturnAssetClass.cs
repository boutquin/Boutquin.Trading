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

/// <summary>
/// The asset class that selects which Ilmanen building-block decomposition
/// <see cref="YieldAnchoredExpectedReturnEstimator"/> applies to a given asset (Ilmanen, A. (2012),
/// <i>Expected Returns on Major Asset Classes</i>, CFA Institute Research Foundation).
/// </summary>
/// <remarks>
/// <para>Ilmanen's expected-return building blocks differ by asset class. <see cref="Bond"/> anchors on the
/// current yield ("the yield on a riskless bond was the expected return of that bond", Foreword, p. vi);
/// <see cref="Equity"/> uses the Gordon-growth / DDM decomposition <c>E[r] = D/P + G_real + inflation</c>
/// (§2.4.3, pp. 28–29; <c>ERP = D/P + G − Y</c>, eq. 1, p. 29). <see cref="YieldAnchoredExpectedReturnEstimator"/>
/// is constructed with a per-symbol map from <see cref="Boutquin.MarketData.Abstractions.ReferenceData.Symbol"/>
/// to this enum, selecting the decomposition applied to each asset (the constructor-bound precedent of
/// <c>CarrySignal.CarryAssetClass</c> / <c>TacticalOverlayConstruction.currentRegime</c>).</para>
/// </remarks>
public enum ExpectedReturnAssetClass
{
    /// <summary>
    /// A bond (fixed-income) sleeve: <c>E[r] ≈ current yield</c>, the latest observation of the asset's own
    /// per-symbol yield series (Ilmanen 2012, Foreword p. vi; the plain current-yield anchor, a documented
    /// V1 simplification of the full <c>BRP_H</c> rule, eq. 3, p. 48).
    /// </summary>
    Bond,

    /// <summary>
    /// An equity or REIT sleeve: <c>E[r] = income (dividend) yield + trend real growth + inflation</c> — the
    /// Gordon-growth / DDM building blocks (Ilmanen 2012, §2.4.3 pp. 28–29). The income yield is the asset's
    /// own per-symbol trailing income-yield series; trend real growth and inflation are market-wide series.
    /// REITs use this same model.
    /// </summary>
    Equity,
}

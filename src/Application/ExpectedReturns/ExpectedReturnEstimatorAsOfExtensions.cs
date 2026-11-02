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
/// Internal dispatch helper for construction models whose two entry points (date-free and date-aware)
/// share one core body: forwards to the estimator's as-of overload when an evaluation date is present,
/// and to the date-free overload when it is not — the estimator mirror of
/// <c>ConstructionModelAsOfExtensions</c>.
/// </summary>
internal static class ExpectedReturnEstimatorAsOfExtensions
{
    /// <summary>
    /// Estimates expected returns, threading <paramref name="asOf"/> through to the estimator's
    /// date-aware overload when present.
    /// </summary>
    internal static decimal[] EstimateExpectedReturnsAsOf(
        this IExpectedReturnEstimator estimator,
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        DateOnly? asOf)
        => asOf is { } date
            ? estimator.EstimateExpectedReturns(assets, returns, auxiliarySeries: null, date)
            : estimator.EstimateExpectedReturns(assets, returns);
}

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

namespace Boutquin.Trading.Application.PortfolioConstruction;

/// <summary>
/// Internal dispatch helper for decorators whose two entry points (date-free and date-aware) share one
/// core body: forwards to the inner model's as-of overload when an evaluation date is present, and to
/// the date-free overload when it is not — so a date-free outer call never fabricates a date, and a
/// date-aware outer call never drops one.
/// </summary>
internal static class ConstructionModelAsOfExtensions
{
    /// <summary>
    /// Computes the inner model's target weights, threading <paramref name="asOf"/> through to the
    /// date-aware overload when present.
    /// </summary>
    internal static IReadOnlyDictionary<Symbol, decimal> ComputeTargetWeightsAsOf(
        this IPortfolioConstructionModel model,
        IReadOnlyList<Symbol> assets,
        decimal[][] returns,
        DateOnly? asOf)
        => asOf is { } date
            ? model.ComputeTargetWeights(assets, returns, date)
            : model.ComputeTargetWeights(assets, returns);
}

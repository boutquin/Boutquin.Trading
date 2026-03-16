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

namespace Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Represents the result of a Brinson-Fachler performance attribution analysis.
/// Decomposes active return into allocation, selection, and interaction effects.
/// </summary>
/// <param name="AllocationEffect">The return attributable to over/underweighting asset classes relative to the benchmark.</param>
/// <param name="SelectionEffect">The return attributable to security selection within each asset class.</param>
/// <param name="InteractionEffect">The cross-product of allocation and selection differences.</param>
/// <param name="TotalActiveReturn">The total active return (portfolio return minus benchmark return).</param>
/// <param name="AssetAllocationEffects">Per-asset allocation effects.</param>
/// <param name="AssetSelectionEffects">Per-asset selection effects.</param>
/// <param name="AssetInteractionEffects">Per-asset interaction effects.</param>
public sealed record BrinsonFachlerResult(
    decimal AllocationEffect,
    decimal SelectionEffect,
    decimal InteractionEffect,
    decimal TotalActiveReturn,
    IReadOnlyDictionary<string, decimal> AssetAllocationEffects,
    IReadOnlyDictionary<string, decimal> AssetSelectionEffects,
    IReadOnlyDictionary<string, decimal> AssetInteractionEffects);

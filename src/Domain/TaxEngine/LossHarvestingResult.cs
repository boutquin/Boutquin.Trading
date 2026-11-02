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

namespace Boutquin.Trading.Domain.TaxEngine;

/// <summary>
/// Result of evaluating a loss harvesting rule (wash sale or superficial loss).
/// </summary>
/// <param name="IsLossDisallowed">Whether the loss is disallowed by the rule.</param>
/// <param name="DisallowedAmount">The disallowed portion of the loss, as currency-tagged <see cref="Money"/>.</param>
/// <param name="AllowedAmount">The allowed (deductible) portion of the loss, as currency-tagged <see cref="Money"/>.</param>
/// <param name="Rule">The name of the rule that was evaluated.</param>
/// <remarks>
/// The amount fields are currency-tagged <see cref="Money"/>; combining them with amounts of a
/// different currency uses <see cref="Money"/> arithmetic, which throws
/// <see cref="Boutquin.Domain.Exceptions.CurrencyMismatchException"/> at the caller.
/// <see cref="IsLossDisallowed"/> and <see cref="Rule"/> stay non-monetary.
/// When <see cref="IsLossDisallowed"/> is <see langword="false"/>, consumers do not consult
/// <see cref="Disposition"/>. Within the current rule scope, acquisition in an IRA or Roth IRA is
/// the sole wash-sale case that uses <see cref="DisallowedLossDisposition.PermanentlyLost"/>;
/// other US wash sales and Canadian superficial losses add the loss to replacement basis.
/// The original positional constructor remains the JSON constructor, so existing four-field
/// payloads remain readable; the optional disposition is restored through its init property.
/// </remarks>
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record LossHarvestingResult(
    bool IsLossDisallowed,
    Money DisallowedAmount,
    Money AllowedAmount,
    string Rule)
{
    /// <summary>
    /// Destination to which a disallowed loss is carried.
    /// </summary>
    public DisallowedLossDisposition Disposition { get; init; } =
        DisallowedLossDisposition.AddedToReplacementBasis;

    /// <summary>
    /// Initializes a result with an explicit disallowed-loss disposition.
    /// </summary>
    /// <param name="IsLossDisallowed">Whether the loss is disallowed by the rule.</param>
    /// <param name="DisallowedAmount">The disallowed portion of the loss.</param>
    /// <param name="AllowedAmount">The allowed portion of the loss.</param>
    /// <param name="Rule">The name of the rule that was evaluated.</param>
    /// <param name="disposition">The destination of the disallowed loss.</param>
    public LossHarvestingResult(
        bool IsLossDisallowed,
        Money DisallowedAmount,
        Money AllowedAmount,
        string Rule,
        DisallowedLossDisposition disposition)
        : this(IsLossDisallowed, DisallowedAmount, AllowedAmount, Rule)
    {
        Disposition = disposition;
    }
}

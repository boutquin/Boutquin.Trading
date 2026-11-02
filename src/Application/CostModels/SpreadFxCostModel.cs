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

namespace Boutquin.Trading.Application.CostModels;

/// <summary>
/// Charges a fixed spread on every FX conversion, expressed in basis points of the amount sold:
/// <c>commission = sellAmount × spreadBps / 10,000</c>, in the sell currency.
/// </summary>
/// <remarks>
/// Retail brokerages quote FX as a spread around the interbank mid rather than as an explicit fee;
/// charging it on the sell side is equivalent to filling at a rate worse than mid by the same
/// proportion. Zero basis points model a free conversion — the default for
/// <c>SimulatedBrokerage</c>, which keeps every single-currency result unchanged.
/// </remarks>
public sealed class SpreadFxCostModel : IFxCostModel
{
    private readonly decimal _spreadBps;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadFxCostModel"/> class.
    /// </summary>
    /// <param name="spreadBps">The spread in basis points of the sold amount. Zero is a free conversion.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="spreadBps"/> is negative.</exception>
    public SpreadFxCostModel(decimal spreadBps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spreadBps);
        _spreadBps = spreadBps;
    }

    /// <summary>A free conversion: zero basis points.</summary>
    public static SpreadFxCostModel Free { get; } = new(0m);

    /// <inheritdoc />
    public decimal CalculateCommission(Currency sellCurrency, decimal sellAmount, Currency buyCurrency, decimal buyAmount) =>
        sellAmount * _spreadBps / 10_000m;
}

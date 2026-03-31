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
/// Specifies a Black-Litterman view by asset name (not array index).
/// Used by <see cref="DynamicBlackLittermanConstruction"/> for DynamicUniverse compatibility
/// where the number of assets changes at each rebalance date.
/// </summary>
public sealed record BlackLittermanViewSpec
{
    /// <summary>Absolute (single asset) or Relative (long-short pair).</summary>
    public BlackLittermanViewType Type { get; }

    /// <summary>For absolute views: the single asset ticker.</summary>
    public string? Asset { get; }

    /// <summary>For relative views: the overweight asset ticker.</summary>
    public string? LongAsset { get; }

    /// <summary>For relative views: the underweight asset ticker.</summary>
    public string? ShortAsset { get; }

    /// <summary>The expected return (absolute) or return spread (relative).</summary>
    public decimal ExpectedReturn { get; }

    /// <summary>View confidence in (0, 1]. Higher = more certain.</summary>
    public decimal Confidence { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackLittermanViewSpec"/> record.
    /// </summary>
    /// <param name="Type">Absolute (single asset) or Relative (long-short pair).</param>
    /// <param name="Asset">For absolute views: the single asset ticker.</param>
    /// <param name="LongAsset">For relative views: the overweight asset ticker.</param>
    /// <param name="ShortAsset">For relative views: the underweight asset ticker.</param>
    /// <param name="ExpectedReturn">The expected return (absolute) or return spread (relative).</param>
    /// <param name="Confidence">View confidence in (0, 1]. Higher = more certain.</param>
    public BlackLittermanViewSpec(
        BlackLittermanViewType Type,
        string? Asset,
        string? LongAsset,
        string? ShortAsset,
        decimal ExpectedReturn,
        decimal Confidence)
    {
        if (Confidence is <= 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(Confidence), Confidence,
                "Confidence must be in the range (0, 1].");
        }

        this.Type = Type;
        this.Asset = Asset;
        this.LongAsset = LongAsset;
        this.ShortAsset = ShortAsset;
        this.ExpectedReturn = ExpectedReturn;
        this.Confidence = Confidence;
    }
}

/// <summary>
/// The type of a Black-Litterman investor view.
/// </summary>
public enum BlackLittermanViewType
{
    /// <summary>An absolute view on a single asset's expected return.</summary>
    Absolute,

    /// <summary>A relative view on the return spread between two assets.</summary>
    Relative,
}

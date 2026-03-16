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

namespace Boutquin.Trading.Application.SlippageModels;

using Domain.ValueObjects;

/// <summary>
/// Models bid-ask spread as slippage using configurable per-asset half-spreads.
/// The fill price is adjusted by the half-spread: buy orders pay mid + halfSpread,
/// sell orders receive mid - halfSpread. Assets without a specific spread use the default.
/// </summary>
public sealed class SpreadSlippage : ISlippageModel
{
    private readonly IReadOnlyDictionary<Asset, decimal> _halfSpreads;
    private readonly decimal _defaultHalfSpread;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadSlippage"/> class.
    /// </summary>
    /// <param name="halfSpreads">Per-asset half-spread percentages (e.g., 0.0001 for 1 basis point).</param>
    /// <param name="defaultHalfSpread">Default half-spread for assets not in the dictionary.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="defaultHalfSpread"/> is negative or zero.</exception>
    public SpreadSlippage(IReadOnlyDictionary<Asset, decimal> halfSpreads, decimal defaultHalfSpread)
    {
        Guard.AgainstNull(() => halfSpreads);
        Guard.AgainstNegativeOrZero(() => defaultHalfSpread);

        _halfSpreads = halfSpreads;
        _defaultHalfSpread = defaultHalfSpread;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This overload does not have asset context. Use <see cref="CalculateFillPriceForAsset"/> when
    /// the asset is known. Falls back to the default half-spread.
    /// </remarks>
    public decimal CalculateFillPrice(decimal theoreticalPrice, int quantity, TradeAction tradeAction) =>
        CalculateFillPriceForAsset(theoreticalPrice, quantity, tradeAction, _defaultHalfSpread);

    /// <summary>
    /// Calculates the fill price for a specific asset, using its configured half-spread if available.
    /// </summary>
    /// <param name="theoreticalPrice">The theoretical execution price.</param>
    /// <param name="quantity">The number of shares/units being traded.</param>
    /// <param name="tradeAction">Whether this is a buy or sell trade.</param>
    /// <param name="asset">The asset being traded.</param>
    /// <returns>The adjusted fill price after applying the bid-ask spread.</returns>
    public decimal CalculateFillPriceForAsset(decimal theoreticalPrice, int quantity, TradeAction tradeAction, Asset asset)
    {
        var halfSpread = _halfSpreads.GetValueOrDefault(asset, _defaultHalfSpread);
        return CalculateFillPriceForAsset(theoreticalPrice, quantity, tradeAction, halfSpread);
    }

    private static decimal CalculateFillPriceForAsset(decimal theoreticalPrice, int quantity, TradeAction tradeAction, decimal halfSpread) =>
        tradeAction == TradeAction.Buy
            ? theoreticalPrice * (1m + halfSpread)
            : theoreticalPrice * (1m - halfSpread);
}

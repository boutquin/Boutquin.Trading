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

/// <summary>
/// Models volume-aware slippage inspired by zipline's VolumeShareSlippage.
/// <para>
/// Two constraints:
/// 1. Volume limit: only a fraction of the bar's volume can be consumed (prevents
///    unrealistic fills in illiquid names). Orders exceeding the limit are partially
///    filled up to the allowed quantity.
/// 2. Price impact: slippage is proportional to the fraction of bar volume consumed.
///    impact = priceImpact * (quantity / barVolume). Buy orders pay more, sell orders receive less.
/// </para>
/// </summary>
public sealed class VolumeShareSlippage : ISlippageModel
{
    private readonly decimal _volumeLimit;
    private readonly decimal _priceImpact;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeShareSlippage"/> class.
    /// </summary>
    /// <param name="volumeLimit">Maximum fraction of bar volume that can be consumed (e.g., 0.025 for 2.5%). Default: 0.025.</param>
    /// <param name="priceImpact">Price impact coefficient. Slippage = priceImpact * (qty / volume). Default: 0.1 (10 bps at 100% fill).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when parameters are out of range.</exception>
    public VolumeShareSlippage(decimal volumeLimit = 0.025m, decimal priceImpact = 0.1m)
    {
        Guard.AgainstNegativeOrZero(() => volumeLimit);
        Guard.AgainstNegativeOrZero(() => priceImpact);

        if (volumeLimit > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(volumeLimit), volumeLimit,
                "Volume limit must be between 0 (exclusive) and 1 (inclusive).");
        }

        _volumeLimit = volumeLimit;
        _priceImpact = priceImpact;
    }

    /// <summary>
    /// Gets the maximum fraction of bar volume that can be consumed.
    /// </summary>
    public decimal VolumeLimit => _volumeLimit;

    /// <summary>
    /// Gets the price impact coefficient.
    /// </summary>
    public decimal PriceImpact => _priceImpact;

    /// <inheritdoc />
    /// <remarks>
    /// Without volume context, applies the price impact assuming the order consumes 100% of available
    /// volume. Use the volume-aware overload for realistic slippage.
    /// </remarks>
    public decimal CalculateFillPrice(decimal theoreticalPrice, int quantity, TradeAction tradeAction)
    {
        // Without volume, assume full impact
        var slippagePct = _priceImpact;
        return tradeAction == TradeAction.Buy
            ? theoreticalPrice * (1m + slippagePct)
            : theoreticalPrice * (1m - slippagePct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Computes slippage proportional to the fraction of bar volume consumed:
    /// slippage% = priceImpact * (quantity / barVolume).
    /// The quantity is capped at volumeLimit * barVolume.
    /// </remarks>
    public decimal CalculateFillPrice(decimal theoreticalPrice, int quantity, TradeAction tradeAction, long barVolume)
    {
        if (barVolume <= 0)
        {
            // Zero-volume bar — no fill possible. Return theoretical price; caller should reject.
            return theoreticalPrice;
        }

        // Cap quantity at volume limit
        var maxQuantity = (int)(barVolume * _volumeLimit);
        var effectiveQuantity = Math.Min(quantity, Math.Max(maxQuantity, 1));

        // Price impact proportional to fraction of volume consumed
        var volumeFraction = (decimal)effectiveQuantity / barVolume;
        var slippagePct = _priceImpact * volumeFraction;

        return tradeAction == TradeAction.Buy
            ? theoreticalPrice * (1m + slippagePct)
            : theoreticalPrice * (1m - slippagePct);
    }
}

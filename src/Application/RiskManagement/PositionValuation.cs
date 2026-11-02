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

namespace Boutquin.Trading.Application.RiskManagement;

/// <summary>
/// Values a position in the portfolio's base currency at the decision date, so a risk rule compares
/// like with like: its current NAV denominator is in base currency, and a foreign-listed asset's
/// <c>quantity × AdjustedClose</c> is not.
/// </summary>
internal static class PositionValuation
{
    /// <summary>
    /// Returns <c>|quantity × price|</c> converted into the portfolio's base currency at the FX rate of
    /// the decision date.
    /// </summary>
    /// <param name="portfolio">The portfolio whose base currency, asset currencies and FX tables apply.</param>
    /// <param name="date">The decision date used for the FX lookup.</param>
    /// <param name="asset">The asset being valued.</param>
    /// <param name="quantity">The (signed) quantity; the result is its absolute value.</param>
    /// <param name="price">The asset's price in its own currency.</param>
    /// <exception cref="InvalidOperationException">Thrown when the asset is foreign-listed and the decision date carries no rate for its currency.</exception>
    public static decimal AbsoluteValueInBase(IPortfolio portfolio, DateOnly date, Symbol asset, int quantity, decimal price)
    {
        var nativeValue = Math.Abs(quantity * price);
        var assetCurrency = portfolio.GetAssetCurrency(asset);
        if (assetCurrency == portfolio.BaseCurrency)
        {
            return nativeValue;
        }

        return FxConversion.ToBase(nativeValue, assetCurrency, portfolio.BaseCurrency, portfolio.HistoricalFxConversionRates, date);
    }

    /// <summary>
    /// Weight of a single share of <paramref name="asset"/> in the portfolio: one share valued in base
    /// currency at <paramref name="price"/> (see <see cref="AbsoluteValueInBase"/>) divided by
    /// <paramref name="totalPortfolioValue"/>. This is the finest increment at which a cap on the asset
    /// can be honoured with whole shares; position sizing rounds share counts with
    /// <see cref="MidpointRounding.AwayFromZero"/>, so a target exactly at a cap overshoots it by at most
    /// half of this weight.
    /// </summary>
    /// <param name="portfolio">The portfolio whose base currency, asset currencies and FX tables apply.</param>
    /// <param name="date">The decision date used for the FX lookup.</param>
    /// <param name="asset">The asset whose share is weighed.</param>
    /// <param name="price">The asset's price in its own currency.</param>
    /// <param name="totalPortfolioValue">The portfolio's total value in base currency; must be positive.</param>
    /// <returns>The base-currency value of one share divided by the portfolio's total value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the asset is foreign-listed and the decision date carries no rate for its currency.</exception>
    public static decimal OneShareWeight(IPortfolio portfolio, DateOnly date, Symbol asset, decimal price, decimal totalPortfolioValue) =>
        AbsoluteValueInBase(portfolio, date, asset, 1, price) / totalPortfolioValue;
}

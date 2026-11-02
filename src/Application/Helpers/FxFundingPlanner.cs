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

namespace Boutquin.Trading.Application.Helpers;

/// <summary>
/// Turns the asset orders of one signal into the FX conversions that fund them, per
/// <see cref="FxFundingPolicy"/>. Shared by <see cref="EventHandlers.SignalEventHandler"/> and
/// <see cref="EventHandlers.RebalancingSignalEventHandler"/>, which emit the returned
/// <see cref="FxOrderEvent"/>s ahead of the asset orders so the brokerage fills them between that
/// bar's asset sells and asset buys.
/// </summary>
/// <remarks>
/// <para>
/// For every currency the strategy trades or holds, the <b>need</b> is
/// <c>Σ buy cost − Σ expected sell proceeds − cash</c>, at signal-bar prices. A positive need is a
/// shortfall, a negative need a surplus. Conversions are planned in three passes: a non-base
/// shortfall is bought with base currency (which adds to the base need); a base shortfall is bought
/// with non-base surpluses, largest first; and, under
/// <see cref="FxFundingPolicy.FundShortfallAndRepatriateSurplus"/>, every remaining non-base surplus
/// is converted back to base. Conversions between two non-base currencies therefore always go
/// through base, as two orders.
/// </para>
/// <para>
/// Amounts are estimates: the fill bar's prices and rate decide what actually clears, and
/// <see cref="EventHandlers.FxFillEventHandler"/> clips a conversion to the sell-currency cash the
/// strategy then holds. Every order's <see cref="FxOrderEvent.BuyAmount"/> is denominated in the
/// currency bought, so a shortfall order asks for exactly the shortfall.
/// </para>
/// </remarks>
public static class FxFundingPlanner
{
    /// <summary>
    /// Plans the FX conversions that fund one signal's asset orders.
    /// </summary>
    /// <param name="timestamp">The signal bar.</param>
    /// <param name="strategy">The strategy whose cash buckets fund the orders.</param>
    /// <param name="baseCurrency">The portfolio's base currency.</param>
    /// <param name="netTradeCashFlows">
    /// Per currency, the cash the signal's asset orders are expected to move at signal-bar prices:
    /// sell proceeds positive, buy costs negative. Currencies with no orders may be omitted.
    /// </param>
    /// <param name="historicalFxConversionRates">The FX map (base units per 1 unit of each non-base currency), read at <paramref name="timestamp"/>.</param>
    /// <param name="policy">Which shortfalls and surpluses to convert.</param>
    /// <returns>The conversions to emit, in execution order; empty when nothing needs converting.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a conversion needs a rate the FX map lacks on <paramref name="timestamp"/>.</exception>
    public static IReadOnlyList<FxOrderEvent> Plan(
        DateOnly timestamp,
        IStrategy strategy,
        Currency baseCurrency,
        IReadOnlyDictionary<Currency, decimal> netTradeCashFlows,
        IReadOnlyDictionary<DateOnly, SortedDictionary<Currency, decimal>> historicalFxConversionRates,
        FxFundingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(netTradeCashFlows);
        ArgumentNullException.ThrowIfNull(historicalFxConversionRates);

        if (policy == FxFundingPolicy.None)
        {
            return [];
        }

        // Shortfall (> 0) or surplus (< 0) per currency after the signal's orders settle.
        var need = new SortedDictionary<Currency, decimal>();
        foreach (var currency in netTradeCashFlows.Keys.Concat(strategy.Cash.Keys).Append(baseCurrency).Distinct())
        {
            need[currency] = -netTradeCashFlows.GetValueOrDefault(currency, 0m) - strategy.Cash.GetValueOrDefault(currency, 0m);
        }

        var orders = new List<FxOrderEvent>();

        // Pass 1 — buy each non-base shortfall with base currency. The base cash that spends is
        // added to the base need so pass 2 can cover it from surpluses.
        foreach (var currency in need.Keys.Where(c => c != baseCurrency && need[c] > 0m).ToList())
        {
            var shortfall = need[currency];
            orders.Add(new FxOrderEvent(timestamp, strategy.Name, baseCurrency, currency, shortfall));
            need[baseCurrency] += FxConversion.ToBase(shortfall, currency, baseCurrency, historicalFxConversionRates, timestamp);
            need[currency] = 0m;
        }

        // Pass 2 — buy a base shortfall with non-base surpluses, largest (in base terms) first.
        if (need[baseCurrency] > 0m)
        {
            var surpluses = need
                .Where(kvp => kvp.Key != baseCurrency && kvp.Value < 0m)
                .Select(kvp => (Currency: kvp.Key, SurplusInBase: FxConversion.ToBase(-kvp.Value, kvp.Key, baseCurrency, historicalFxConversionRates, timestamp)))
                .OrderByDescending(s => s.SurplusInBase)
                .ThenBy(s => s.Currency)
                .ToList();

            foreach (var (currency, surplusInBase) in surpluses)
            {
                var take = Math.Min(surplusInBase, need[baseCurrency]);
                orders.Add(new FxOrderEvent(timestamp, strategy.Name, currency, baseCurrency, take));
                need[baseCurrency] -= take;
                need[currency] += FxConversion.FromBase(take, currency, baseCurrency, historicalFxConversionRates, timestamp);

                if (need[baseCurrency] <= 0m)
                {
                    break;
                }
            }
        }

        // Pass 3 — repatriate what is left in non-base currencies.
        if (policy == FxFundingPolicy.FundShortfallAndRepatriateSurplus)
        {
            foreach (var currency in need.Keys.Where(c => c != baseCurrency && need[c] < 0m).ToList())
            {
                var surplusInBase = FxConversion.ToBase(-need[currency], currency, baseCurrency, historicalFxConversionRates, timestamp);
                orders.Add(new FxOrderEvent(timestamp, strategy.Name, currency, baseCurrency, surplusInBase));
                need[currency] = 0m;
            }
        }

        return orders;
    }
}

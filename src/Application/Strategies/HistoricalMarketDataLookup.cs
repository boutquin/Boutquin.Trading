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

namespace Boutquin.Trading.Application.Strategies;

/// <summary>
/// Cross-calendar lookup helpers for historical market data. When a portfolio's trading
/// calendar differs from an individual asset's listing calendar, some timestamps in the
/// outer date index may have no bar for that specific asset — e.g. a CA blueprint marking
/// to market on a TSX trading day that falls inside the 2001-09-11 → 2001-09-14 NYSE
/// closure will have no VTI bar even though other assets in the portfolio do.
/// </summary>
/// <remarks>
/// These helpers forward-fill by walking backward from the target timestamp up to
/// <see cref="MostRecentBarLookbackDays"/> calendar days and returning the most recent
/// available bar. This matches real-world marking-to-market where a foreign-exchange-closed
/// asset's last close is carried forward. When the lookback exhausts without finding a bar
/// (asset not yet incepted, or gap longer than the window), the caller decides whether to
/// throw or treat the position as zero-value.
/// </remarks>
internal static class HistoricalMarketDataLookup
{
    /// <summary>
    /// Maximum calendar-day lookback when searching for an asset's most recent bar prior
    /// to the target timestamp. Set wide enough to span the longest documented regular-market
    /// closure on any major exchange: the post-9/11 NYSE closure (2001-09-11 → 2001-09-17,
    /// 4 closed trading days inside a 6-day window) is the empirical driver; 14 days gives
    /// a safety margin for weekend-adjacent multi-day closures and end-of-year holidays.
    /// </summary>
    internal const int MostRecentBarLookbackDays = 14;

    /// <summary>
    /// Walks backward from <paramref name="timestamp"/> up to <see cref="MostRecentBarLookbackDays"/>
    /// calendar days to find the most recent <see cref="Bar"/> for <paramref name="asset"/>.
    /// Returns <see langword="null"/> when no prior bar exists within the window — asset
    /// not yet incepted, or cross-calendar gap wider than the lookback.
    /// </summary>
    internal static Bar? TryFindMostRecentBar(
        IReadOnlyDictionary<DateOnly, SortedDictionary<Symbol, Bar>> historicalMarketData,
        Symbol asset,
        DateOnly timestamp)
    {
        var priorDate = timestamp;
        for (var i = 0; i < MostRecentBarLookbackDays; i++)
        {
            priorDate = priorDate.AddDays(-1);
            if (historicalMarketData.TryGetValue(priorDate, out var priorDayData) &&
                priorDayData is not null &&
                priorDayData.TryGetValue(asset, out var priorBar))
            {
                return priorBar;
            }
        }

        return null;
    }
}

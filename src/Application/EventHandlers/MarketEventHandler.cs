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

namespace Boutquin.Trading.Application.EventHandlers;

/// <summary>
/// The MarketEventHandler class is an implementation of the IEventHandler interface that handles MarketEvent objects.
/// MarketEvent objects represent the market data for a specific financial asset at a specific point in time.
/// </summary>
/// <remarks>
/// This class handles MarketEvent objects by updating the historical data, positions, and cash of the portfolio.
/// The IPortfolio object that is passed to the MarketEventHandler constructor is used to update the portfolio state.
/// 
/// Here is an example of how to use this class:
/// <code>
/// var portfolio = new Portfolio();
/// var marketEventHandler = new MarketEventHandler();
/// 
/// var marketEvent = new MarketEvent();
/// await marketEventHandler.HandleEventAsync(portfolio, marketEvent);
/// </code>
/// </remarks>
public sealed class MarketEventHandler : IEventHandler
{
    /// <summary>
    /// Handles the provided MarketEvent object.
    /// </summary>
    /// <param name="portfolio">The portfolio for which the event is relevant.</param>
    /// <param name="eventObj">The MarketEvent object to handle.</param>
    /// <param name="cancellationToken">A token to cancel the async operation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when portfolio is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when eventObj is not a MarketEvent object.</exception>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The HandleEventAsync method updates the historical data, positions, and cash of the portfolio based on the MarketEvent object.
    /// The portfolio is retrieved from the portfolio that was passed to the MarketEventHandler constructor.
    /// </remarks>
    public async Task HandleEventAsync(IPortfolio portfolio, IFinancialEvent eventObj, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(() => portfolio); // Throws ArgumentNullException

        var marketEvent = eventObj as MarketEvent
            ?? throw new ArgumentException("Event must be of type MarketEvent.", nameof(eventObj));

        cancellationToken.ThrowIfCancellationRequested();

        // Call methods on the Portfolio class to perform the necessary actions
        portfolio.UpdateHistoricalData(marketEvent);

        // Detect and handle dividend and split events.
        // MarketEvent may contain data for assets across multiple portfolios (e.g. portfolio + benchmark).
        // Skip assets that don't belong to this portfolio to avoid "Asset not found" errors.
        foreach (var (asset, marketData) in marketEvent.HistoricalMarketData)
        {
            if (portfolio.AssetCurrencies != null && !portfolio.AssetCurrencies.ContainsKey(asset))
            {
                continue;
            }

            // Detect and handle dividend events.
            // In backtesting mode (IsLive == false), AdjustedClose already incorporates
            // dividends — the price series does NOT drop on ex-dividend dates. Crediting
            // dividendPerShare as cash would double-count every dividend, creating phantom
            // value that compounds through rebalancing. ETFs with high cumulative dividend
            // yield (e.g. SCHH with a 2.47x Close/AdjustedClose ratio) accumulate enough
            // phantom cash to produce impossible daily returns (< -100%).
            // Only credit dividends in live mode where raw Close is used for valuation.
            var dividendPerShare = marketData.DividendPerShare;
            if (portfolio.IsLive && dividendPerShare > 0)
            {
                portfolio.UpdateCashForDividend(asset, dividendPerShare, marketData.Close);
            }

            // Detect and handle split events.
            // In backtesting mode (IsLive == false), AdjustedClose in the CSV already
            // accounts for splits — adjusting positions would double-count the split,
            // causing a spurious ~100% daily return on the split date.
            // Only adjust positions (and historical data) in live mode where the
            // portfolio holds actual pre-split share counts.
            var splitCoefficient = marketData.SplitCoefficient;
            // BUG-A06: Guard zero split coefficient and skip no-split case
            if (splitCoefficient is 0 or 1)
            {
                continue;
            }

            if (portfolio.IsLive)
            {
                portfolio.AdjustPositionForSplit(asset, splitCoefficient);
                portfolio.AdjustHistoricalDataForSplit(asset, splitCoefficient);
            }
        }

        // A4 fix: Capture GenerateSignals return value and feed each signal into the event processor
        var signals = portfolio.GenerateSignals(marketEvent);
        foreach (var signal in signals)
        {
            await portfolio.HandleEventAsync(signal, cancellationToken).ConfigureAwait(false);
        }
    }
}

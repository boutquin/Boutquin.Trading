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

namespace Boutquin.Trading.Domain.Helpers;

/// <summary>
/// The MarketDataFileNameHelper class provides methods for generating file names for market data and FX rate data.
/// </summary>
/// <remarks>
/// This class provides static methods for generating file names for market data and FX rate data.
/// The file names are generated based on a directory and a ticker or currency pair.
/// The ticker or currency pair is sanitized to remove or replace any characters that are not valid in file names.
/// </remarks>
public static class MarketDataFileNameHelper
{
    /// <summary>
    /// Generates a CSV file name for market data.
    /// </summary>
    /// <param name="directory">The directory where the file will be located.</param>
    /// <param name="ticker">The ticker for the market data.</param>
    /// <returns>The full path to the CSV file (not a relative filename).</returns>
    public static string GetCsvFileNameForMarketData(string directory, string ticker)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(ticker);

        var sanitized = SanitizeTickerForFileName(ticker);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException(
                "Ticker produced an empty filename after sanitization.", nameof(ticker));
        }

        return Path.Combine(directory, "daily_adjusted_" + sanitized + ".csv");
    }

    /// <summary>
    /// Sanitizes a ticker for use in a file name.
    /// </summary>
    /// <param name="ticker">The ticker to sanitize.</param>
    /// <returns>A string representing the sanitized ticker.</returns>
    // Cross-platform set of chars that are invalid in filenames (superset of all OS restrictions)
    private static readonly HashSet<char> s_invalidFileNameChars = new(
        Path.GetInvalidFileNameChars()
            .Concat(['<', '>', ':', '"', '|', '?', '*', '\\', '/'])
            .Concat(['^']));

    private static string SanitizeTickerForFileName(string ticker)
    {
        // C2 fix: Strip path separators, ".." traversal, and all invalid filename chars
        var sanitized = new string(ticker.Where(c => !s_invalidFileNameChars.Contains(c)).ToArray());

        // Strip ".." sequences that survived char filtering
        sanitized = sanitized.Replace("..", "");

        return sanitized;
    }

    /// <summary>
    /// Generates a CSV file name for FX rate data.
    /// </summary>
    /// <param name="directory">The directory where the file will be located.</param>
    /// <param name="currencyPair">The currency pair for the FX rate data.</param>
    /// <returns>The full path to the CSV file (not a relative filename).</returns>
    public static string GetCsvFileNameForFxRateData(string directory, string currencyPair)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(currencyPair);

        var sanitized = SanitizeCurrencyPairForFileName(currencyPair);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException(
                "Currency pair produced an empty filename after sanitization.",
                nameof(currencyPair));
        }

        return Path.Combine(directory, "daily_fx_" + sanitized + ".csv");
    }

    /// <summary>
    /// Sanitizes a currency pair for use in a file name.
    /// </summary>
    /// <param name="currencyPair">The currency pair to sanitize.</param>
    /// <returns>A string representing the sanitized currency pair.</returns>
    private static string SanitizeCurrencyPairForFileName(string currencyPair)
    {
        // Replace slash with underscore, then strip all invalid filename chars
        var sanitized = new string(
            currencyPair.Select(c => c == '/' ? '_' : c)
                        .Where(c => !s_invalidFileNameChars.Contains(c))
                        .ToArray());
        sanitized = sanitized.Replace("..", "");
        return sanitized;
    }
}

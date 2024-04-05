// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
namespace Boutquin.Trading.Data.CSV;

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
    /// <returns>A string representing the file name.</returns>
    /// <remarks>
    /// This method generates a CSV file name for market data based on a directory and a ticker.
    /// The ticker is sanitized to remove or replace any characters that are not valid in file names.
    /// The file name is in the format "daily_adjusted_{ticker}.csv".
    /// </remarks>
    public static string GetCsvFileNameForMarketData(string directory, string ticker)
    {
        return Path.Combine(directory, "daily_adjusted_" + SanitizeTickerForFileName(ticker) + ".csv");
    }

    /// <summary>
    /// Sanitizes a ticker for use in a file name.
    /// </summary>
    /// <param name="ticker">The ticker to sanitize.</param>
    /// <returns>A string representing the sanitized ticker.</returns>
    /// <remarks>
    /// This method sanitizes a ticker for use in a file name by removing or replacing any characters that are not valid in file names.
    /// In this case, the caret symbol is removed.
    /// </remarks>
    private static string SanitizeTickerForFileName(string ticker)
    {
        // Replace the caret symbol with an underscore
        // return ticker.Replace('^', '_');

        // Or remove the caret symbol
        return ticker.Replace("^", "");
    }

    /// <summary>
    /// Generates a CSV file name for FX rate data.
    /// </summary>
    /// <param name="directory">The directory where the file will be located.</param>
    /// <param name="currencyPair">The currency pair for the FX rate data.</param>
    /// <returns>A string representing the file name.</returns>
    /// <remarks>
    /// This method generates a CSV file name for FX rate data based on a directory and a currency pair.
    /// The currency pair is sanitized to remove or replace any characters that are not valid in file names.
    /// The file name is in the format "daily_fx_{currencyPair}.csv".
    /// </remarks>
    public static string GetCsvFileNameForFxRateData(string directory, string currencyPair)
    {
        return Path.Combine(directory, "daily_fx_" + SanitizeCurrencyPairForFileName(currencyPair) + ".csv");
    }

    /// <summary>
    /// Sanitizes a currency pair for use in a file name.
    /// </summary>
    /// <param name="currencyPair">The currency pair to sanitize.</param>
    /// <returns>A string representing the sanitized currency pair.</returns>
    /// <remarks>
    /// This method sanitizes a currency pair for use in a file name by removing or replacing any characters that are not valid in file names.
    /// In this case, the slash symbol is replaced with an underscore.
    /// </remarks>
    private static string SanitizeCurrencyPairForFileName(string currencyPair)
    {
        // Replace the slash symbol with an underscore
        return currencyPair.Replace("/", "_");
    }
}

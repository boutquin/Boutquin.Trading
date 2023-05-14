// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Domain.Helpers;

public static class MarketDataFileNameHelper
{
    public static string GetCsvFileNameForMarketData(string directory, string ticker)
    {
        return Path.Combine(directory, "daily_adjusted_" + SanitizeTickerForFileName(ticker) + ".csv");
    }

    private static string SanitizeTickerForFileName(string ticker)
    {
        // Replace the caret symbol with an underscore
        // return ticker.Replace('^', '_');

        // Or remove the caret symbol
        return ticker.Replace("^", "");
    }

    public static string GetCsvFileNameForFxRateData(string directory, string currencyPair)
    {
        return Path.Combine(directory, "daily_fx_" + SanitizeCurrencyPairForFileName(currencyPair) + ".csv");
    }

    private static string SanitizeCurrencyPairForFileName(string currencyPair)
    {
        // Replace the slash symbol with an underscore
        return currencyPair.Replace("/", "_");
    }
}

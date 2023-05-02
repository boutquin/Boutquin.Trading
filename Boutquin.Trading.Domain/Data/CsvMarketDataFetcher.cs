﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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

using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Helpers;
using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Data;

public sealed class CsvMarketDataFetcher : IMarketDataFetcher
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>> FetchMarketDataAsync(IEnumerable<string> symbols)
    {
        if (symbols == null || !symbols.Any())
        {
            throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
        }

        foreach (var symbol in symbols)
        {
            var fileName = MarketDataFileNameHelper.GetCsvFileNameForMarketData(symbol);

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Market data file not found for symbol {symbol}.", fileName);
            }

            var marketData = new SortedDictionary<DateOnly, MarketData>();

            await using var fileStream = File.OpenRead(fileName);
            using var streamReader = new StreamReader(fileStream);

            // Skip the header row
            await streamReader.ReadLineAsync();

            while (await streamReader.ReadLineAsync() is { } line)
            {
                KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>? dataPoint = null;
                Exception? dataException = null;

                try
                {
                    var columns = line.Split(',');

                    var date = DateOnly.Parse(columns[0]);
                    var open = decimal.Parse(columns[1]);
                    var high = decimal.Parse(columns[2]);
                    var low = decimal.Parse(columns[3]);
                    var close = decimal.Parse(columns[4]);
                    var adjustedClose = decimal.Parse(columns[5]);
                    var volume = long.Parse(columns[6]);
                    var dividendPerShare = decimal.Parse(columns[7]);
                    var splitCoefficient = decimal.Parse(columns[8]);

                    var marketDataPoint = new MarketData(date, open, high, low, close, adjustedClose, volume, dividendPerShare, splitCoefficient);
                    marketData[date] = marketDataPoint;

                    dataPoint = new KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>(date, new SortedDictionary<string, MarketData> { { symbol, marketDataPoint } });
                }
                catch (FormatException ex)
                {
                    dataException = new InvalidDataException($"Invalid data format in CSV file '{fileName}' for symbol '{symbol}'", ex);
                }
                catch (OverflowException ex)
                {
                    dataException = new InvalidDataException($"Numeric value out of range in CSV file '{fileName}' for symbol '{symbol}'", ex);
                }
                catch (IndexOutOfRangeException ex)
                {
                    dataException = new InvalidDataException($"Invalid column count in CSV file '{fileName}' for symbol '{symbol}'", ex);
                }

                if (dataException != null)
                {
                    throw new MarketDataRetrievalException($"Error reading CSV file '{fileName}' for symbol '{symbol}'", dataException);
                }

                if (dataPoint.HasValue)
                {
                    yield return dataPoint.Value;
                }
            }
        }
    }
}

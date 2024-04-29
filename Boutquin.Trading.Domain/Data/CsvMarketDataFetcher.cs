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
namespace Boutquin.Trading.Domain.Data;

using ValueObjects;

using Exceptions;

/// <summary>
/// Fetches market data from CSV files.
/// </summary>
/// <remarks>
/// This class is responsible for fetching market data from CSV files. The CSV files should be located in the directory specified during the object creation.
/// The CSV files should have a specific format. For market data, the format is: Date,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient.
/// For FX rates, the format is: Date,Rate.
/// 
/// Here is a sample usage of this class:
/// <code>
/// var fetcher = new CsvMarketDataFetcher("path/to/your/directory");
/// await foreach (var data in fetcher.FetchMarketDataAsync(new[] { new Asset("AAPL"), new Asset("MSFT") }))
/// {
///     // Process data
/// }
/// await foreach (var rate in fetcher.FetchFxRatesAsync(new[] { "USD_EUR", "USD_GBP" }))
/// {
///     // Process rate
/// }
/// </code>
/// </remarks>
public sealed class CsvMarketDataFetcher : IMarketDataFetcher
{
    // The directory where the CSV files will be stored
    private readonly string _dataDirectory;

    public CsvMarketDataFetcher(string directory)
    {
        _dataDirectory = directory ?? throw new ArgumentNullException(nameof(directory));

        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<ValueObjects.Asset, MarketData>?>> FetchMarketDataAsync(
        IEnumerable<ValueObjects.Asset> symbols)
    {
        if (symbols == null || !symbols.Any())
        {
            throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
        }

        foreach (var symbol in symbols)
        {
            var fileName = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_dataDirectory, symbol.Ticker);

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
                KeyValuePair<DateOnly, SortedDictionary<ValueObjects.Asset, MarketData>?>? dataPoint = null;
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

                    var marketDataPoint = new MarketData(date, open, high, low, close, adjustedClose, volume,
                        dividendPerShare, splitCoefficient);
                    marketData[date] = marketDataPoint;

                    dataPoint = new KeyValuePair<DateOnly, SortedDictionary<ValueObjects.Asset, MarketData>>(date,
                        new SortedDictionary<ValueObjects.Asset, MarketData> { { symbol, marketDataPoint } });
                }
                catch (FormatException ex)
                {
                    dataException =
                        new InvalidDataException($"Invalid data format in CSV file '{fileName}' for symbol '{symbol}'",
                            ex);
                }
                catch (OverflowException ex)
                {
                    dataException =
                        new InvalidDataException(
                            $"Numeric value out of range in CSV file '{fileName}' for symbol '{symbol}'", ex);
                }
                catch (IndexOutOfRangeException ex)
                {
                    dataException =
                        new InvalidDataException($"Invalid column count in CSV file '{fileName}' for symbol '{symbol}'",
                            ex);
                }

                if (dataException != null)
                {
                    throw new MarketDataRetrievalException($"Error reading CSV file '{fileName}' for symbol '{symbol}'",
                        dataException);
                }

                if (dataPoint.HasValue)
                {
                    yield return dataPoint.Value;
                }
            }
        }

    }

    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(
        IEnumerable<string> currencyPairs)
    {
        if (currencyPairs == null || !currencyPairs.Any())
        {
            throw new ArgumentException("At least one currency pair must be provided.", nameof(currencyPairs));
        }

        foreach (var pair in currencyPairs)
        {
            // Split the pair into base and quote currency codes
            var splitPair = pair.Split('_');
            if (splitPair.Length != 2 || !Enum.TryParse<CurrencyCode>(splitPair[1], out var quoteCurrencyCode))
            {
                throw new ArgumentException($"Invalid currency pair: {pair}", nameof(currencyPairs));
            }

            var fileName = MarketDataFileNameHelper.GetCsvFileNameForFxRateData(_dataDirectory, pair);

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"FX rate data file not found for currency pair {pair}.", fileName);
            }

            var fxRates = new SortedDictionary<DateOnly, decimal>();

            await using var fileStream = File.OpenRead(fileName);
            using var streamReader = new StreamReader(fileStream);

            // Skip the header row
            await streamReader.ReadLineAsync();

            while (await streamReader.ReadLineAsync() is { } line)
            {
                KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>? dataPoint = null;
                Exception? dataException = null;

                try
                {
                    var columns = line.Split(',');

                    var date = DateOnly.Parse(columns[0]);
                    var rate = decimal.Parse(columns[1]);

                    fxRates[date] = rate;

                    dataPoint = new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(date,
                        new SortedDictionary<CurrencyCode, decimal> { { quoteCurrencyCode, rate } });
                }
                catch (FormatException ex)
                {
                    dataException =
                        new InvalidDataException(
                            $"Invalid data format in CSV file '{fileName}' for currency pair '{pair}'", ex);
                }
                catch (OverflowException ex)
                {
                    dataException =
                        new InvalidDataException(
                            $"Numeric value out of range in CSV file '{fileName}' for currency pair '{pair}'", ex);
                }
                catch (IndexOutOfRangeException ex)
                {
                    dataException =
                        new InvalidDataException(
                            $"Invalid column count in CSV file '{fileName}' for currency pair '{pair}'", ex);
                }

                if (dataException != null)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for currency pair '{pair}'", dataException);
                }

                if (dataPoint.HasValue)
                {
                    yield return dataPoint.Value;
                }
            }
        }
    }
}

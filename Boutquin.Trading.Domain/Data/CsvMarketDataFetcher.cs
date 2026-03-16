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

namespace Boutquin.Trading.Domain.Data;

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

        // ERR-D02: Wrap Directory.CreateDirectory in try-catch to surface OS errors.
        if (!Directory.Exists(_dataDirectory))
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new MarketDataStorageException(
                    $"Failed to create data directory '{_dataDirectory}'.", ex);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<ValueObjects.Asset, MarketData>>> FetchMarketDataAsync(
        IEnumerable<ValueObjects.Asset> symbols, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // PERF-D01: Materialize to avoid double enumeration of IEnumerable.
        var symbolList = (symbols ?? throw new ArgumentNullException(nameof(symbols))).ToList();
        if (symbolList.Count == 0)
        {
            throw new ArgumentException("At least one symbol must be provided.", nameof(symbols));
        }

        foreach (var symbol in symbolList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_dataDirectory, symbol.Ticker);

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Market data file not found for symbol {symbol}.", fileName);
            }

            var marketData = new SortedDictionary<DateOnly, MarketData>();

#pragma warning disable CA2007
            await using var fileStream = File.OpenRead(fileName);
#pragma warning restore CA2007
            using var streamReader = new StreamReader(fileStream);

            // Skip the header row
            await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            // ERR-D03: Throw directly from catch instead of deferred-exception pattern.
            while (await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                var columns = line.Split(',');

                DateOnly date;
                MarketData marketDataPoint;
                try
                {
                    date = DateOnly.Parse(columns[0], System.Globalization.CultureInfo.InvariantCulture);
                    var open = decimal.Parse(columns[1], System.Globalization.CultureInfo.InvariantCulture);
                    var high = decimal.Parse(columns[2], System.Globalization.CultureInfo.InvariantCulture);
                    var low = decimal.Parse(columns[3], System.Globalization.CultureInfo.InvariantCulture);
                    var close = decimal.Parse(columns[4], System.Globalization.CultureInfo.InvariantCulture);
                    var adjustedClose = decimal.Parse(columns[5], System.Globalization.CultureInfo.InvariantCulture);
                    var volume = long.Parse(columns[6], System.Globalization.CultureInfo.InvariantCulture);
                    var dividendPerShare = decimal.Parse(columns[7], System.Globalization.CultureInfo.InvariantCulture);
                    var splitCoefficient = decimal.Parse(columns[8], System.Globalization.CultureInfo.InvariantCulture);

                    marketDataPoint = new MarketData(date, open, high, low, close, adjustedClose, volume,
                        dividendPerShare, splitCoefficient);
                    marketData[date] = marketDataPoint;
                }
                catch (FormatException ex)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for symbol '{symbol}'",
                        new InvalidDataException($"Invalid data format in CSV file '{fileName}' for symbol '{symbol}'", ex));
                }
                catch (OverflowException ex)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for symbol '{symbol}'",
                        new InvalidDataException($"Numeric value out of range in CSV file '{fileName}' for symbol '{symbol}'", ex));
                }
                catch (IndexOutOfRangeException ex)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for symbol '{symbol}'",
                        new InvalidDataException($"Invalid column count in CSV file '{fileName}' for symbol '{symbol}'", ex));
                }

                yield return new KeyValuePair<DateOnly, SortedDictionary<ValueObjects.Asset, MarketData>>(date,
                    new SortedDictionary<ValueObjects.Asset, MarketData> { { symbol, marketDataPoint } });
            }
        }

    }

    public async IAsyncEnumerable<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> FetchFxRatesAsync(
        IEnumerable<string> currencyPairs, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // PERF-D01: Materialize to avoid double enumeration of IEnumerable.
        var pairList = (currencyPairs ?? throw new ArgumentNullException(nameof(currencyPairs))).ToList();
        if (pairList.Count == 0)
        {
            throw new ArgumentException("At least one currency pair must be provided.", nameof(currencyPairs));
        }

        foreach (var pair in pairList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ROB-D02: Validate both base and quote currency parts of the pair.
            var splitPair = pair.Split('_');
            if (splitPair.Length != 2
                || !Enum.TryParse<CurrencyCode>(splitPair[0], out _)
                || !Enum.TryParse<CurrencyCode>(splitPair[1], out var quoteCurrencyCode))
            {
                throw new ArgumentException($"Invalid currency pair: {pair}", nameof(currencyPairs));
            }

            var fileName = MarketDataFileNameHelper.GetCsvFileNameForFxRateData(_dataDirectory, pair);

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"FX rate data file not found for currency pair {pair}.", fileName);
            }

            var fxRates = new SortedDictionary<DateOnly, decimal>();

#pragma warning disable CA2007
            await using var fileStream = File.OpenRead(fileName);
#pragma warning restore CA2007
            using var streamReader = new StreamReader(fileStream);

            // Skip the header row
            await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            // ERR-D03: Throw directly from catch instead of deferred-exception pattern.
            while (await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                var columns = line.Split(',');

                DateOnly date;
                decimal rate;
                try
                {
                    date = DateOnly.Parse(columns[0], System.Globalization.CultureInfo.InvariantCulture);
                    rate = decimal.Parse(columns[1], System.Globalization.CultureInfo.InvariantCulture);
                    fxRates[date] = rate;
                }
                catch (FormatException ex)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for currency pair '{pair}'",
                        new InvalidDataException($"Invalid data format in CSV file '{fileName}' for currency pair '{pair}'", ex));
                }
                catch (OverflowException ex)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for currency pair '{pair}'",
                        new InvalidDataException($"Numeric value out of range in CSV file '{fileName}' for currency pair '{pair}'", ex));
                }
                catch (IndexOutOfRangeException ex)
                {
                    throw new MarketDataRetrievalException(
                        $"Error reading CSV file '{fileName}' for currency pair '{pair}'",
                        new InvalidDataException($"Invalid column count in CSV file '{fileName}' for currency pair '{pair}'", ex));
                }

                yield return new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(date,
                    new SortedDictionary<CurrencyCode, decimal> { { quoteCurrencyCode, rate } });
            }
        }
    }
}

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
namespace Boutquin.Trading.Domain.Data;

using System.Security;

using Exceptions;

using Helpers;

using Interfaces;

public sealed class CsvMarketDataStorage : IMarketDataStorage
{
    // The directory where the CSV files will be stored
    private readonly string _dataDirectory;

    public CsvMarketDataStorage(string directory)
    {
        _dataDirectory = directory ?? throw new ArgumentNullException(nameof(directory));

        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task SaveMarketDataAsync(KeyValuePair<DateOnly, SortedDictionary<string, MarketData>?> dataPoint)
    {
        // Validate the input data point
        if (dataPoint.Value == null || dataPoint.Value.Count == 0)
        {
            throw new ArgumentException("The data point must contain at least one symbol and its market data.", nameof(dataPoint));
        }

        // Iterate through each symbol and its corresponding market data
        foreach (var symbolData in dataPoint.Value)
        {
            var symbol = symbolData.Key;
            var marketData = symbolData.Value;
            var fileName = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_dataDirectory, symbol);
            var filePath = Path.Combine(_dataDirectory, fileName);

            try
            {
                // Check if the file exists, and create it with the header if not
                if (!File.Exists(filePath))
                {
                    await using var fileStream = File.Create(filePath);
                    await using var streamWriter = new StreamWriter(fileStream);

                    await streamWriter.WriteLineAsync("Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient");
                }

                // Append the data point to the file
                await using var appendFileStream = File.Open(filePath, FileMode.Append, FileAccess.Write);
                await using var appendStreamWriter = new StreamWriter(appendFileStream);

                var line = $"{marketData.Timestamp},{marketData.Open},{marketData.High},{marketData.Low},{marketData.Close},{marketData.AdjustedClose},{marketData.Volume},{marketData.DividendPerShare},{marketData.SplitCoefficient}";
                await appendStreamWriter.WriteLineAsync(line);
            }
            catch (IOException ex)
            {
                throw new MarketDataStorageException($"Error writing CSV file '{filePath}' for symbol '{symbol}'", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new MarketDataStorageException($"Access denied to CSV file '{filePath}' for symbol '{symbol}'", ex);
            }
            catch (SecurityException ex)
            {
                throw new MarketDataStorageException($"Security error accessing CSV file '{filePath}' for symbol '{symbol}'", ex);
            }
        }
    }


    /// <inheritdoc/>
    public async Task SaveMarketDataAsync(IEnumerable<KeyValuePair<DateOnly, SortedDictionary<string, MarketData>>> dataPoints)
    {
        if (dataPoints == null)
        {
            throw new ArgumentNullException(nameof(dataPoints));
        }

        // Group the data points by symbol
        var groupedDataPoints = dataPoints.SelectMany(x => x.Value)
            .GroupBy(x => x.Key, x => x.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var symbolDataPoints in groupedDataPoints)
        {
            var symbol = symbolDataPoints.Key;
            var fileName = MarketDataFileNameHelper.GetCsvFileNameForMarketData(_dataDirectory, symbol);
            var filePath = Path.Combine(_dataDirectory, fileName);

            try
            {
                // Check if the file exists, and create it with the header if not
                if (!File.Exists(filePath))
                {
                    await using var fileStream = File.Create(filePath);
                    using var streamWriter = new StreamWriter(fileStream);

                    await streamWriter.WriteLineAsync("Timestamp,Open,High,Low,Close,AdjustedClose,Volume,DividendPerShare,SplitCoefficient");
                }

                // Append the data points to the file
                await using var appendFileStream = File.Open(filePath, FileMode.Append, FileAccess.Write);
                await using var appendStreamWriter = new StreamWriter(appendFileStream);

                foreach (var dataPoint in symbolDataPoints.Value)
                {
                    var line = $"{dataPoint.Timestamp},{dataPoint.Open},{dataPoint.High},{dataPoint.Low},{dataPoint.Close},{dataPoint.AdjustedClose},{dataPoint.Volume},{dataPoint.DividendPerShare},{dataPoint.SplitCoefficient}";
                    await appendStreamWriter.WriteLineAsync(line);
                }
            }
            catch (IOException ex)
            {
                throw new MarketDataStorageException($"Error writing CSV file '{filePath}' for symbol '{symbol}'", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new MarketDataStorageException($"Access denied to CSV file '{filePath}' for symbol '{symbol}'", ex);
            }
            catch (SecurityException ex)
            {
                throw new MarketDataStorageException($"Security error accessing CSV file '{filePath}' for symbol '{symbol}'", ex);
            }
        }
    }
}

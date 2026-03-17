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
/// Stores economic data series to CSV files.
/// Uses atomic write (tmp + rename) since FRED data is overwrite-mode.
/// CSV format: Date,Value
/// </summary>
public sealed class CsvEconomicDataStorage
{
    private readonly string _dataDirectory;

    public CsvEconomicDataStorage(string directory)
    {
        _dataDirectory = directory ?? throw new ArgumentNullException(nameof(directory));

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

    /// <summary>
    /// Saves an economic data series to CSV with atomic write.
    /// Overwrites existing file (FRED data is fetched as complete series).
    /// </summary>
    public async Task SaveSeriesAsync(
        string seriesId,
        IAsyncEnumerable<KeyValuePair<DateOnly, decimal>> data,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(() => seriesId);
        ArgumentNullException.ThrowIfNull(data);

        var filePath = GetCsvFileName(seriesId);
        var tmpPath = filePath + ".tmp";

        try
        {
            await using var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(fileStream);
            await writer.WriteLineAsync("Date,Value").ConfigureAwait(false);

            await foreach (var kvp in data.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var line = FormattableString.Invariant($"{kvp.Key},{kvp.Value}");
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
            throw;
        }

        File.Move(tmpPath, filePath, overwrite: true);
    }

    public string GetCsvFileName(string seriesId) =>
        Path.Combine(_dataDirectory, $"fred_{SanitizeForFileName(seriesId)}.csv");

    private static string SanitizeForFileName(string input)
    {
        var invalidChars = new HashSet<char>(
            Path.GetInvalidFileNameChars()
                .Concat(['<', '>', ':', '"', '|', '?', '*', '\\', '/']));
        return new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
    }
}

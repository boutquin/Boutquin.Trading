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

using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Boutquin.Trading.Domain.Enums;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Data.FamaFrench;

/// <summary>
/// Fetches factor return data from the Kenneth R. French Data Library.
/// Downloads ZIP-compressed CSV files containing daily or monthly factor returns
/// for the standard academic risk factors (Mkt-RF, SMB, HML, RMW, CMA, Mom, RF).
/// Values are returned in percentage form as the source provides them.
/// </summary>
public sealed class FamaFrenchFetcher : IFactorDataFetcher, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;
    private readonly string _baseUrl;

    private static readonly IReadOnlyDictionary<FamaFrenchDataset, string> s_monthlyStems =
        new Dictionary<FamaFrenchDataset, string>
        {
            [FamaFrenchDataset.ThreeFactors] = "F-F_Research_Data_Factors",
            [FamaFrenchDataset.FiveFactors] = "F-F_Research_Data_5_Factors_2x3",
            [FamaFrenchDataset.Momentum] = "F-F_Momentum_Factor",
        };

    private static readonly IReadOnlyDictionary<FamaFrenchDataset, string> s_dailyStems =
        new Dictionary<FamaFrenchDataset, string>
        {
            [FamaFrenchDataset.ThreeFactors] = "F-F_Research_Data_Factors_daily",
            [FamaFrenchDataset.FiveFactors] = "F-F_Research_Data_5_Factors_2x3_daily",
            [FamaFrenchDataset.Momentum] = "F-F_Momentum_Factor_daily",
        };

    public FamaFrenchFetcher(
        HttpClient? httpClient = null,
        string baseUrl = "https://mba.tuck.dartmouth.edu/pages/faculty/ken.french/ftp")
    {
        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchDailyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDataset(dataset);

        var stem = s_dailyStems[dataset];
        return FetchCoreAsync(stem, isDaily: true, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> FetchMonthlyAsync(
        FamaFrenchDataset dataset,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        ValidateDataset(dataset);

        var stem = s_monthlyStems[dataset];
        return FetchCoreAsync(stem, isDaily: false, startDate, endDate, cancellationToken);
    }

    private async IAsyncEnumerable<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>>
        FetchCoreAsync(
            string urlStem,
            bool isDaily,
            DateOnly? startDate,
            DateOnly? endDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = $"{_baseUrl}/{urlStem}_CSV.zip";
        string csvContent;

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();

            using var responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;

            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault()
                ?? throw new MarketDataRetrievalException("ZIP archive is empty");

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            csvContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MarketDataRetrievalException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new MarketDataRetrievalException(
                $"Failed to fetch Fama-French data from '{url}'.", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new MarketDataRetrievalException(
                $"Failed to decompress Fama-French data from '{url}'.", ex);
        }

        var lines = csvContent.Split('\n');

        // Find header line: first line starting with ","
        var headerIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith(','))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            throw new MarketDataRetrievalException("No header line found in CSV");
        }

        var headerParts = lines[headerIndex].Split(',');
        var factorNames = new string[headerParts.Length - 1];
        for (var i = 1; i < headerParts.Length; i++)
        {
            factorNames[i - 1] = headerParts[i].Trim();
        }

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // Stop at blank line (monthly annual section follows) or copyright
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (line.TrimStart().StartsWith("Copyright", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parts = line.Split(',');
            if (parts.Length < factorNames.Length + 1)
            {
                continue;
            }

            var dateField = parts[0].Trim();
            DateOnly date;

            if (isDaily)
            {
                if (dateField.Length != 8)
                {
                    continue;
                }

                if (!DateOnly.TryParseExact(dateField, "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out date))
                {
                    continue;
                }
            }
            else
            {
                if (dateField.Length != 6)
                {
                    continue;
                }

                if (!int.TryParse(dateField[..4], out var year) ||
                    !int.TryParse(dateField[4..6], out var month) ||
                    year < 1 || month < 1 || month > 12)
                {
                    continue;
                }

                date = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            }

            // Apply date range filter
            if (startDate.HasValue && date < startDate.Value)
            {
                continue;
            }

            if (endDate.HasValue && date > endDate.Value)
            {
                continue;
            }

            // Parse factor values
            var factors = new Dictionary<string, decimal>(factorNames.Length);
            var hasMissing = false;

            for (var j = 0; j < factorNames.Length; j++)
            {
                if (!decimal.TryParse(parts[j + 1].Trim(), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var value))
                {
                    hasMissing = true;
                    break;
                }

                if (value == -99.99m || value == -999m)
                {
                    hasMissing = true;
                    break;
                }

                factors[factorNames[j]] = value;
            }

            if (hasMissing)
            {
                continue;
            }

            yield return new KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>(date, factors);
        }
    }

    private static void ValidateDataset(FamaFrenchDataset dataset)
    {
        if (!Enum.IsDefined(dataset))
        {
            throw new ArgumentOutOfRangeException(nameof(dataset), dataset,
                "Undefined FamaFrenchDataset value.");
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}

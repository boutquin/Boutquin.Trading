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

using System.Text.Json.Serialization;

namespace Boutquin.Trading.Data.TwelveData.Responses;

/// <summary>
/// Top-level response from the Twelve Data /time_series endpoint.
/// </summary>
public sealed record TwelveDataTimeSeriesResponse(
    [property: JsonPropertyName("values")] TwelveDataTimeSeriesValue[]? Values,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message);

/// <summary>
/// A single OHLCV data point from the Twelve Data time_series response.
/// All numeric fields are returned as strings by the API.
/// </summary>
public sealed record TwelveDataTimeSeriesValue(
    [property: JsonPropertyName("datetime")] string Datetime,
    [property: JsonPropertyName("open")] string Open,
    [property: JsonPropertyName("high")] string High,
    [property: JsonPropertyName("low")] string Low,
    [property: JsonPropertyName("close")] string Close,
    [property: JsonPropertyName("volume")] string? Volume);

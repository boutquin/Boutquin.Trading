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
/// Top-level response from the Twelve Data /splits endpoint.
/// </summary>
public sealed record TwelveDataSplitsResponse(
    [property: JsonPropertyName("splits")] TwelveDataSplitEntry[]? Splits,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("message")] string? Message);

/// <summary>
/// A single split record from the Twelve Data splits response.
/// The ratio field is a string like "4:1" meaning 4 new shares for every 1 old share.
/// </summary>
public sealed record TwelveDataSplitEntry(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("ratio")] string? Ratio,
    [property: JsonPropertyName("from_factor")] decimal? FromFactor,
    [property: JsonPropertyName("to_factor")] decimal? ToFactor);

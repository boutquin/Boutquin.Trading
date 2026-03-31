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

namespace Boutquin.Trading.Data.Frankfurter.Responses;

/// <summary>
/// DTO for Frankfurter date-range response.
/// </summary>
public sealed record FrankfurterRangeResponse(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("base")] string Base,
    [property: JsonPropertyName("start_date")] string StartDate,
    [property: JsonPropertyName("end_date")] string EndDate,
    [property: JsonPropertyName("rates")] Dictionary<string, Dictionary<string, decimal>> Rates);

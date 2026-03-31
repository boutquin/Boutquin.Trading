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

namespace Boutquin.Trading.Data.Tiingo.Responses;

/// <summary>
/// DTO for a single day's price data from the Tiingo EOD endpoint.
/// All 13 fields are always present in the response.
/// </summary>
public sealed record TiingoDailyPrice(
    DateTimeOffset Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal AdjOpen,
    decimal AdjHigh,
    decimal AdjLow,
    decimal AdjClose,
    long AdjVolume,
    decimal DivCash,
    decimal SplitFactor);

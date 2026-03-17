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

namespace Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Represents a single drawdown period with full details: start, trough, recovery, depth, and duration.
/// </summary>
/// <param name="StartDate">The date the drawdown began (peak before decline).</param>
/// <param name="TroughDate">The date of the maximum depth within this drawdown.</param>
/// <param name="RecoveryDate">The date the equity recovered to the prior peak, or null if still in drawdown.</param>
/// <param name="Depth">The maximum percentage decline from peak (negative value, e.g., -0.15 = -15%).</param>
/// <param name="DurationDays">Number of calendar days from start to recovery (or to the last date if not recovered).</param>
/// <param name="RecoveryDays">Number of calendar days from trough to recovery, or null if not recovered.</param>
public sealed record DrawdownPeriod(
    DateOnly StartDate,
    DateOnly TroughDate,
    DateOnly? RecoveryDate,
    decimal Depth,
    int DurationDays,
    int? RecoveryDays);

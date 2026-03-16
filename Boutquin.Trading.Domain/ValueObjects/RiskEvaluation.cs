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

namespace Boutquin.Trading.Domain.ValueObjects;

/// <summary>
/// Represents the result of a risk evaluation, indicating whether an order is allowed.
/// </summary>
/// <param name="IsAllowed">Whether the order is allowed by the risk rule(s).</param>
/// <param name="RejectionReason">The reason for rejection, or null if allowed.</param>
public sealed record RiskEvaluation(
    bool IsAllowed,
    string? RejectionReason = null)
{
    /// <summary>
    /// Creates a passing evaluation result.
    /// </summary>
    public static RiskEvaluation Allowed { get; } = new(true);

    /// <summary>
    /// Creates a rejection evaluation result with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for rejecting the order.</param>
    /// <returns>A rejected risk evaluation.</returns>
    public static RiskEvaluation Rejected(string reason) => new(false, reason);
}

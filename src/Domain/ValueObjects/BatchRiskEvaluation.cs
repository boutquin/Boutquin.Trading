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
/// Represents the result of evaluating a batch of orders against risk rules.
/// The batch is evaluated as a unit — the projected portfolio state after ALL
/// orders is checked, not each order in isolation.
/// </summary>
/// <param name="IsAllowed">Whether the entire batch is allowed.</param>
/// <param name="RejectionReason">The reason for rejection, or null if allowed.</param>
/// <param name="RejectedOrders">
/// Individual order rejections when the batch is rejected.
/// Empty when the batch is allowed.
/// </param>
public sealed record BatchRiskEvaluation(
    bool IsAllowed,
    string? RejectionReason = null,
    IReadOnlyList<OrderRiskResult>? RejectedOrders = null)
{
    /// <summary>
    /// Creates a passing batch evaluation result.
    /// </summary>
    public static BatchRiskEvaluation Allowed { get; } = new(true);

    /// <summary>
    /// Creates a rejection batch evaluation result with the specified reason.
    /// </summary>
    public static BatchRiskEvaluation Rejected(string reason, IReadOnlyList<OrderRiskResult>? rejectedOrders = null)
    {
        Guard.AgainstNullOrWhiteSpace(() => reason);
        return new(false, reason, rejectedOrders);
    }
}

/// <summary>
/// Associates an order with its individual risk evaluation result within a batch.
/// </summary>
/// <param name="Order">The order that was evaluated.</param>
/// <param name="Evaluation">The risk evaluation result for this order.</param>
public sealed record OrderRiskResult(
    Order Order,
    RiskEvaluation Evaluation);

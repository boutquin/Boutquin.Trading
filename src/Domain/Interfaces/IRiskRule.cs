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

namespace Boutquin.Trading.Domain.Interfaces;

using ValueObjects;

/// <summary>
/// Represents a single risk management rule that evaluates whether an order
/// should be allowed given the current portfolio state.
/// </summary>
public interface IRiskRule
{
    /// <summary>
    /// Gets the name of this risk rule for logging and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates whether the proposed order is acceptable under this rule.
    /// </summary>
    /// <param name="order">The proposed order to evaluate.</param>
    /// <param name="portfolio">The current portfolio state.</param>
    /// <returns>A risk evaluation result indicating whether the order is allowed.</returns>
    RiskEvaluation Evaluate(Order order, IPortfolio portfolio);
}

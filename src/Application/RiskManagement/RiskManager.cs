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

namespace Boutquin.Trading.Application.RiskManagement;

using Domain.ValueObjects;

/// <summary>
/// Composite risk manager that evaluates an order against all registered risk rules.
/// An order must pass all rules to be allowed.
/// </summary>
public sealed class RiskManager : IRiskManager
{
    private readonly IReadOnlyList<IRiskRule> _rules;

    /// <summary>
    /// Initializes a new instance of <see cref="RiskManager"/>.
    /// </summary>
    /// <param name="rules">The risk rules to evaluate against.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
    public RiskManager(IEnumerable<IRiskRule> rules)
    {
        Guard.AgainstNull(() => rules);
        _rules = rules.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public RiskEvaluation Evaluate(Order order, IPortfolio portfolio)
    {
        Guard.AgainstNull(() => order);
        Guard.AgainstNull(() => portfolio);

        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(order, portfolio);
            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return RiskEvaluation.Allowed;
    }
}

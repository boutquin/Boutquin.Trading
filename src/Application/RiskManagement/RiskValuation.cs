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

/// <summary>Obtains causal risk NAV without adding or reading performance reporting rows.</summary>
internal static class RiskValuation
{
    /// <summary>Values current holdings and every currency cash bucket using the portfolio's existing mark/FX contract.</summary>
    /// <param name="portfolio">The current portfolio state; no state is changed.</param>
    /// <param name="date">The decision date, also used for asset marks and FX conversion.</param>
    /// <returns>Positive NAV in the portfolio's base currency.</returns>
    /// <exception cref="InvalidOperationException">The computed NAV is not positive. Missing valuation inputs also propagate their existing errors.</exception>
    public static decimal PositiveNav(IPortfolio portfolio, DateOnly date)
    {
        var value = portfolio.CalculateTotalPortfolioValue(date);
        if (value <= 0m)
        {
            throw new InvalidOperationException(FormattableString.Invariant(
                $"Risk NAV is not positive on {date:yyyy-MM-dd}: {value}. Cannot evaluate risk."));
        }
        return value;
    }
}

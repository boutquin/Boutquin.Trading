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

namespace Boutquin.Trading.Domain.Helpers;

/// <summary>
/// Well-known Fama-French factor names as they appear in the CSV headers.
/// Use these constants when accessing factor values from the dictionary returned by
/// <see cref="Boutquin.Trading.Domain.Interfaces.IFactorDataFetcher"/>.
/// </summary>
public static class FamaFrenchConstants
{
    /// <summary>Market excess return (market return minus risk-free rate).</summary>
    public const string MarketExcessReturn = "Mkt-RF";

    /// <summary>Small Minus Big — size factor.</summary>
    public const string SmallMinusBig = "SMB";

    /// <summary>High Minus Low — value factor (book-to-market).</summary>
    public const string HighMinusLow = "HML";

    /// <summary>Robust Minus Weak — profitability factor (5-factor model).</summary>
    public const string RobustMinusWeak = "RMW";

    /// <summary>Conservative Minus Aggressive — investment factor (5-factor model).</summary>
    public const string ConservativeMinusAggressive = "CMA";

    /// <summary>Risk-free rate (1-month T-Bill).</summary>
    public const string RiskFreeRate = "RF";

    /// <summary>Momentum factor (winners minus losers, 12-1 month).</summary>
    public const string Momentum = "Mom";
}

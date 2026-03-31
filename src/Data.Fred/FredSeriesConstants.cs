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

namespace Boutquin.Trading.Data.Fred;

/// <summary>
/// Well-known FRED series IDs for treasury yields, inflation, and growth indicators.
/// </summary>
public static class FredSeriesConstants
{
    /// <summary>3-Month Treasury Constant Maturity Rate (daily).</summary>
    public const string Treasury3Month = "DGS3MO";

    /// <summary>1-Year Treasury Constant Maturity Rate (daily).</summary>
    public const string Treasury1Year = "DGS1";

    /// <summary>5-Year Treasury Constant Maturity Rate (daily).</summary>
    public const string Treasury5Year = "DGS5";

    /// <summary>10-Year Treasury Constant Maturity Rate (daily).</summary>
    public const string Treasury10Year = "DGS10";

    /// <summary>30-Year Treasury Constant Maturity Rate (daily).</summary>
    public const string Treasury30Year = "DGS30";

    /// <summary>Effective Federal Funds Rate (daily).</summary>
    public const string FedFundsRate = "DFF";

    /// <summary>Consumer Price Index for All Urban Consumers (monthly).</summary>
    public const string CpiAllUrban = "CPIAUCSL";

    /// <summary>CPI less Food and Energy (core inflation, monthly).</summary>
    public const string CpiCoreLessFoodEnergy = "CPILFESL";

    /// <summary>10-Year Breakeven Inflation Rate (daily).</summary>
    public const string BreakevenInflation10Year = "T10YIE";

    /// <summary>Real Gross Domestic Product (quarterly).</summary>
    public const string RealGdp = "GDPC1";

    /// <summary>Industrial Production Index (monthly).</summary>
    public const string IndustrialProduction = "INDPRO";

    /// <summary>Civilian Unemployment Rate (monthly).</summary>
    public const string UnemploymentRate = "UNRATE";

    /// <summary>Total Nonfarm Payrolls (monthly).</summary>
    public const string NonfarmPayrolls = "PAYEMS";
}

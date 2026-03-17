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

namespace Boutquin.Trading.Application.Configuration;

/// <summary>
/// Configuration options for transaction cost and slippage models.
/// Binds to the "CostModel" section of appsettings.json.
/// </summary>
public sealed class CostModelOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "CostModel";

    /// <summary>
    /// The type of transaction cost model (e.g., "FixedPerTrade", "PercentageOfValue", "TieredCommission").
    /// </summary>
    public string TransactionCostType { get; set; } = "FixedPerTrade";

    /// <summary>
    /// The commission rate or fixed amount (interpretation depends on TransactionCostType).
    /// </summary>
    public decimal CommissionRate { get; set; } = 10m;

    /// <summary>
    /// The type of slippage model (e.g., "NoSlippage", "FixedSlippage", "PercentageSlippage").
    /// </summary>
    public string SlippageType { get; set; } = "NoSlippage";

    /// <summary>
    /// The slippage amount (interpretation depends on SlippageType).
    /// </summary>
    public decimal SlippageAmount { get; set; }

    /// <summary>
    /// The default bid-ask spread as a percentage (e.g., 0.0002 for 2 basis points).
    /// </summary>
    public decimal DefaultSpreadPercent { get; set; }
}

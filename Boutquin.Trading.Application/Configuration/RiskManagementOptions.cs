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
/// Configuration options for risk management rules.
/// Binds to the "RiskManagement" section of appsettings.json.
/// </summary>
public sealed class RiskManagementOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "RiskManagement";

    /// <summary>
    /// The maximum allowable drawdown as a decimal (e.g., 0.20 for 20%).
    /// Set to 0 to disable.
    /// </summary>
    public decimal MaxDrawdownPercent { get; set; } = 0.20m;

    /// <summary>
    /// The maximum allowable position size as a fraction of portfolio value.
    /// Set to 0 to disable.
    /// </summary>
    public decimal MaxPositionSizePercent { get; set; } = 0.25m;

    /// <summary>
    /// The maximum allowable exposure per asset class as a fraction of portfolio value.
    /// Set to 0 to disable.
    /// </summary>
    public decimal MaxSectorExposurePercent { get; set; } = 0.40m;
}

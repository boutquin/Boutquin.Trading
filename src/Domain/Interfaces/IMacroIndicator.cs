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

/// <summary>
/// Computes a macro-economic indicator from two related time series.
/// Examples: yield curve slope (10Y - 2Y), breakeven inflation (nominal - TIPS), credit spread (HYG - treasury).
/// </summary>
public interface IMacroIndicator
{
    /// <summary>
    /// Computes the macro indicator from two price/yield series.
    /// </summary>
    /// <param name="series1">First series (e.g., 10Y yields), ordered chronologically.</param>
    /// <param name="series2">Second series (e.g., 2Y yields), ordered chronologically.</param>
    /// <returns>The computed indicator value.</returns>
    decimal Compute(decimal[] series1, decimal[] series2);
}

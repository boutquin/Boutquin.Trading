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

namespace Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Represents the result of a multi-factor regression of portfolio returns
/// against Fama-French (or other) risk factors.
/// </summary>
/// <param name="Alpha">The intercept (alpha) of the regression — unexplained excess return.</param>
/// <param name="FactorLoadings">The beta coefficient for each factor (e.g., "Mkt-Rf" → 1.05).</param>
/// <param name="RSquared">The R² goodness-of-fit (0 to 1).</param>
/// <param name="ResidualStandardError">The standard error of the regression residuals.</param>
public sealed record FactorRegressionResult(
    decimal Alpha,
    IReadOnlyDictionary<string, decimal> FactorLoadings,
    decimal RSquared,
    decimal ResidualStandardError);

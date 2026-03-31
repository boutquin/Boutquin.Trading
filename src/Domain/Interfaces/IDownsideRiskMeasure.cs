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
/// Defines a downside risk measure that can be evaluated and differentiated
/// with respect to portfolio weights. Used by <c>MeanDownsideRiskConstruction</c>
/// to optimize E[r] − λ × Risk(w).
///
/// Implementations may carry internal auxiliary state (e.g., VaR threshold for CVaR)
/// that is updated alongside the weight optimization.
/// </summary>
public interface IDownsideRiskMeasure
{
    /// <summary>
    /// Evaluates the risk measure and its gradient for the given portfolio weights.
    /// </summary>
    /// <param name="weights">Current portfolio weight vector (length N).</param>
    /// <param name="scenarios">
    /// Scenario return matrix: <c>scenarios[t][i]</c> is the return of asset <c>i</c>
    /// in scenario <c>t</c>. Dimensions: S scenarios × N assets.
    /// </param>
    /// <param name="learningRate">
    /// Current learning rate, provided so implementations with auxiliary variables
    /// (e.g., CVaR's ζ) can update them at a compatible step size.
    /// </param>
    /// <returns>
    /// A tuple of (riskValue, gradientWithRespectToWeights).
    /// The gradient array has length N, same as weights.
    /// </returns>
    (decimal Value, decimal[] Gradient) Evaluate(
        decimal[] weights,
        decimal[][] scenarios,
        decimal learningRate);
}

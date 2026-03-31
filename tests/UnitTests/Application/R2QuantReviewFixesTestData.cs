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

namespace Boutquin.Trading.Tests.UnitTests.Application;

/// <summary>
/// Test data for R2Q quantitative review fixes.
/// </summary>
public static class R2QuantReviewFixesTestData
{
    /// <summary>
    /// 3-asset return series with known covariance structure for LedoitWolf tests.
    /// Assets have distinct volatilities and moderate positive correlation.
    /// </summary>
    public static decimal[][] ThreeAssetReturns =>
    [
        [0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m, -0.01m, 0.03m,
         0.01m, -0.02m, 0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m],
        [0.01m, -0.005m, 0.015m, -0.01m, 0.005m, 0.02m, -0.015m, 0.01m, -0.005m, 0.015m,
         0.005m, -0.01m, 0.01m, -0.005m, 0.015m, -0.01m, 0.005m, 0.02m, -0.015m, 0.01m],
        [0.015m, -0.008m, 0.022m, -0.015m, 0.008m, 0.03m, -0.022m, 0.015m, -0.008m, 0.022m,
         0.008m, -0.015m, 0.015m, -0.008m, 0.022m, -0.015m, 0.008m, 0.03m, -0.022m, 0.015m]
    ];

    /// <summary>
    /// Highly correlated 3-asset returns where rho is large,
    /// so (pi - rho) is much smaller than pi.
    /// </summary>
    public static decimal[][] HighCorrelationReturns =>
    [
        [0.01m, -0.01m, 0.02m, -0.02m, 0.01m, 0.03m, -0.01m, 0.02m, -0.01m, 0.01m,
         0.01m, -0.01m, 0.02m, -0.02m, 0.01m, 0.03m, -0.01m, 0.02m, -0.01m, 0.01m],
        [0.011m, -0.009m, 0.021m, -0.019m, 0.011m, 0.031m, -0.009m, 0.021m, -0.009m, 0.011m,
         0.011m, -0.009m, 0.021m, -0.019m, 0.011m, 0.031m, -0.009m, 0.021m, -0.009m, 0.011m],
        [0.0105m, -0.0095m, 0.0205m, -0.0195m, 0.0105m, 0.0305m, -0.0095m, 0.0205m, -0.0095m, 0.0105m,
         0.0105m, -0.0095m, 0.0205m, -0.0195m, 0.0105m, 0.0305m, -0.0095m, 0.0205m, -0.0095m, 0.0105m]
    ];

    /// <summary>
    /// 3-asset returns where one asset (index 2) has strong negative correlation
    /// with the other two, producing negative marginal risk contribution.
    /// </summary>
    public static decimal[][] NegativeMrcReturns =>
    [
        [0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m, -0.01m, 0.03m,
         0.01m, -0.02m, 0.02m, -0.01m, 0.03m, -0.02m, 0.01m, 0.04m, -0.03m, 0.02m],
        [0.025m, -0.015m, 0.035m, -0.025m, 0.015m, 0.045m, -0.035m, 0.025m, -0.015m, 0.035m,
         0.015m, -0.025m, 0.025m, -0.015m, 0.035m, -0.025m, 0.015m, 0.045m, -0.035m, 0.025m],
        [-0.03m, 0.02m, -0.04m, 0.03m, -0.02m, -0.05m, 0.04m, -0.03m, 0.02m, -0.04m,
         -0.02m, 0.03m, -0.03m, 0.02m, -0.04m, 0.03m, -0.02m, -0.05m, 0.04m, -0.03m]
    ];

    /// <summary>
    /// Daily returns with known mean and standard deviation for Monte Carlo Sharpe ratio tests.
    /// Uses a deterministic series so we can predict the annualized Sharpe.
    /// </summary>
    public static decimal[] KnownDailyReturns
    {
        get
        {
            var rng = new Random(12345);
            var returns = new decimal[252];
            for (var i = 0; i < returns.Length; i++)
            {
                // Generates returns with positive mean ~ 0.0004 daily (~ 10% annualized)
                returns[i] = (decimal)(rng.NextDouble() * 0.04 - 0.018);
            }

            return returns;
        }
    }
}

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

using Boutquin.Trading.Application.Analytics;

public sealed class FactorRegressorSingularTests
{
    [Fact]
    public void Regress_CollinearFactors_ThrowsCalculationException()
    {
        // Two identical factor series → singular normal equation matrix
        var portfolioReturns = new decimal[] { 0.01m, 0.02m, -0.01m, 0.03m, 0.00m };
        var factorNames = new List<string> { "Factor1", "Factor2" };
        var factor1 = new decimal[] { 0.005m, 0.010m, -0.005m, 0.015m, 0.000m };
        var factor2 = new decimal[] { 0.005m, 0.010m, -0.005m, 0.015m, 0.000m }; // Identical to factor1

        var act = () => FactorRegressor.Regress(portfolioReturns, factorNames, new[] { factor1, factor2 });

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>()
            .WithMessage("*singular*");
    }
}

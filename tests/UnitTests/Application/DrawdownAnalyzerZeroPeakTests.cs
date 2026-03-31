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

public sealed class DrawdownAnalyzerZeroPeakTests
{
    [Fact]
    public void AnalyzeDrawdownPeriods_PeakIsZero_ThrowsCalculationException()
    {
        var equityCurve = new SortedDictionary<DateOnly, decimal>
        {
            [new DateOnly(2026, 1, 1)] = 0m,
            [new DateOnly(2026, 1, 2)] = -10m,
        };

        var act = () => DrawdownAnalyzer.AnalyzeDrawdownPeriods(equityCurve);

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>()
            .WithMessage("*peak*zero*");
    }
}

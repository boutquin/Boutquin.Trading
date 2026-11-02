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

using Boutquin.Trading.Application.DownsideRisk;
using FluentAssertions;

/// <summary>
/// Pins the Rockafellar–Uryasev (CVaR) / Chekhlov–Uryasev–Zabarankin (CDaR)
/// small-scenario-count regime where <c>S·(1−α) &lt; 1</c>. In that regime the
/// analytical ζ shortcut must still equal the true min-over-ζ R–U value (which
/// degenerates to the single worst loss / drawdown), and the empty-scenario
/// contract must surface as <c>CalculationException</c> across all three siblings.
///
/// Expected values are derived from an independent numpy min-over-ζ R–U
/// computation (not the C# replica), so these tests pin reference fidelity,
/// not self-consistency.
/// </summary>
public sealed class DownsideRiskMeasureSmallSampleTests
{
    private const decimal Precision = 1e-12m;

    // ==================== M-CVAR: small-S R–U minimizer ====================

    [Fact]
    public void CVaR_SmallScenarioCount_EqualsTrueRockafellarUryasevMinimum()
    {
        // S=7, α=0.95 ⇒ S·(1−α) = 0.35 < 1 (integer tail clamps to 1 in the bug).
        // Single asset (weights=[1]) ⇒ portfolio returns == scenario returns.
        // Independent numpy min-over-ζ R–U value = 0.10 (the single worst loss),
        // because when S·(1−α) < 1 the tail mass is below one observation and the
        // R–U objective is minimized at ζ = worst loss.
        //   true CVaR  = 0.10
        //   buggy CVaR = 0.13714285714285714... (ζ picked at second-worst loss,
        //                tailFactor = 1/0.35 > 1 over-weighting the worst exceedance).
        var measure = new CVaRRiskMeasure(0.95m);
        var weights = new[] { 1m };
        var scenarios = new[]
        {
            new[] { 0.04m },
            new[] { 0.03m },
            new[] { -0.06m },
            new[] { -0.065m },
            new[] { -0.07m },
            new[] { -0.08m },
            new[] { -0.10m },
        };

        var (value, _) = measure.Evaluate(weights, scenarios, 0.01m);

        value.Should().BeApproximately(0.10m, Precision,
            "small-S CVaR must equal the true Rockafellar–Uryasev min-over-ζ value");
    }

    // ==================== M-CDAR: small-T R–U minimizer ====================

    [Fact]
    public void CDaR_SmallScenarioCount_EqualsTrueRockafellarUryasevMinimum()
    {
        // T=7, α=0.95 ⇒ T·(1−α) = 0.35 < 1. Single asset ⇒ scenario returns are the
        // portfolio returns; the additive drawdown series (origin-floored peak) is
        //   dd = [0, -0.05, -0.08, -0.07, -0.11, -0.17, -0.15]
        // Independent numpy min-over-ζ R–U-on-drawdowns value = 0.17 (worst drawdown),
        // buggy value = 0.20714285714285714... (ζ at second-worst, tailFactor > 1).
        var measure = new CDaRRiskMeasure(0.95m);
        var weights = new[] { 1m };
        var scenarios = new[]
        {
            new[] { 0.02m },
            new[] { -0.05m },
            new[] { -0.03m },
            new[] { 0.01m },
            new[] { -0.04m },
            new[] { -0.06m },
            new[] { 0.02m },
        };

        var (value, _) = measure.Evaluate(weights, scenarios, 0.01m);

        value.Should().BeApproximately(0.17m, Precision,
            "small-T CDaR must equal the true Rockafellar–Uryasev min-over-ζ value");
    }

    // ==================== M-DDRM: empty-scenario contract ====================

    [Fact]
    public void DownsideDeviation_Evaluate_EmptyScenarios_ThrowsCalculationException()
    {
        // Siblings CVaR/CDaR throw CalculationException on empty scenarios; this
        // sibling must too (per CLAUDE.md degenerate-input convention), not leak a
        // raw DivideByZeroException from `downsideSumSq / s` with s == 0.
        var measure = new DownsideDeviationRiskMeasure(0m);
        var weights = new[] { 1m };
        var scenarios = Array.Empty<decimal[]>();

        var act = () => measure.Evaluate(weights, scenarios, 0.01m);

        act.Should().Throw<Boutquin.Trading.Domain.Exceptions.CalculationException>();
    }
}

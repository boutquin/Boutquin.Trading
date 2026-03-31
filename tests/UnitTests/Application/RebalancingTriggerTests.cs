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

using Boutquin.Trading.Application.Rebalancing;
using Boutquin.Trading.Domain.ValueObjects;
using FluentAssertions;

/// <summary>
/// Tests for rebalancing trigger implementations.
/// </summary>
public sealed class RebalancingTriggerTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");

    // --- ThresholdRebalancingTrigger Tests ---

    [Fact]
    public void Threshold_AllWithinBand_ShouldNotRebalance()
    {
        var trigger = new ThresholdRebalancingTrigger(0.05m);
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m, [s_tlt] = 0.30m, [s_gld] = 0.20m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 0.52m, [s_tlt] = 0.28m, [s_gld] = 0.20m };

        var result = trigger.ShouldRebalance(current, target);

        result.Should().BeFalse("All assets are within ±5% band");
    }

    [Fact]
    public void Threshold_SingleAssetBeyondBand_ShouldRebalance()
    {
        var trigger = new ThresholdRebalancingTrigger(0.05m);
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m, [s_tlt] = 0.30m, [s_gld] = 0.20m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 0.56m, [s_tlt] = 0.26m, [s_gld] = 0.18m };

        var result = trigger.ShouldRebalance(current, target);

        result.Should().BeTrue("VTI drifted +6%, exceeding ±5% band");
    }

    [Fact]
    public void Threshold_ExactlyAtBand_ShouldNotRebalance()
    {
        var trigger = new ThresholdRebalancingTrigger(0.05m);
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 0.55m };

        var result = trigger.ShouldRebalance(current, target);

        result.Should().BeFalse("Drift equals threshold exactly, not exceeds");
    }

    [Fact]
    public void Threshold_AfterRebalance_DriftIsZero()
    {
        var trigger = new ThresholdRebalancingTrigger(0.05m);
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m, [s_tlt] = 0.50m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 0.50m, [s_tlt] = 0.50m };

        var result = trigger.ShouldRebalance(current, target);

        result.Should().BeFalse("After rebalance, weights match target exactly");
    }

    [Fact]
    public void Threshold_ZeroOrNegativeThreshold_ShouldThrow()
    {
        var actZero = () => new ThresholdRebalancingTrigger(0m);
        var actNeg = () => new ThresholdRebalancingTrigger(-0.01m);

        actZero.Should().Throw<ArgumentOutOfRangeException>();
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Threshold_MissingAssetInCurrent_ShouldTreatAsZero()
    {
        var trigger = new ThresholdRebalancingTrigger(0.05m);
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m, [s_tlt] = 0.50m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 1.00m };

        var result = trigger.ShouldRebalance(current, target);

        result.Should().BeTrue("TLT has current weight 0 vs target 0.50, drift = 0.50");
    }

    [Fact]
    public void Threshold_ExtraAssetInCurrent_ShouldTriggerIfSignificant()
    {
        var trigger = new ThresholdRebalancingTrigger(0.05m);
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m, [s_tlt] = 0.50m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 0.40m, [s_tlt] = 0.40m, [s_gld] = 0.20m };

        var result = trigger.ShouldRebalance(current, target);

        result.Should().BeTrue("GLD has 20% weight but target is 0%, drift = 20%");
    }

    // --- CalendarRebalancingTrigger Tests ---

    [Fact]
    public void Calendar_AlwaysReturnsTrue()
    {
        var trigger = new CalendarRebalancingTrigger();
        var target = new Dictionary<Asset, decimal> { [s_vti] = 0.50m };
        var current = new Dictionary<Asset, decimal> { [s_vti] = 0.50m };

        trigger.ShouldRebalance(current, target).Should().BeTrue();
    }
}

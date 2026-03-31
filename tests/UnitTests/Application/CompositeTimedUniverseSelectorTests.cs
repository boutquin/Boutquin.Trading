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

using Boutquin.Trading.Application.Universe;
using Boutquin.Trading.Domain.Analytics;

public sealed class CompositeTimedUniverseSelectorTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_tlt = new("TLT");
    private static readonly Asset s_gld = new("GLD");
    private static readonly Asset s_spy = new("SPY");

    // ============================================================
    // Basic composition with AND logic
    // ============================================================

    [Fact]
    public void SelectAsOf_WithTwoFilters_ShouldApplyBothAsAnd()
    {
        // Filter 1: MinAum 500M (excludes GLD with 100M)
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = new(s_vti, AumMillions: 1000m, InceptionDate: new DateOnly(2010, 1, 1), AverageDailyVolume: 5_000_000m),
            [s_tlt] = new(s_tlt, AumMillions: 800m, InceptionDate: new DateOnly(2012, 1, 1), AverageDailyVolume: 3_000_000m),
            [s_gld] = new(s_gld, AumMillions: 100m, InceptionDate: new DateOnly(2015, 1, 1), AverageDailyVolume: 1_000_000m),
        };

        var aumFilter = new MinAumFilter(500m, metadata);

        // Filter 2: DynamicUniverse (VTI available from 2010, TLT from 2012)
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_tlt] = new DateOnly(2012, 1, 1),
            [s_gld] = new DateOnly(2015, 1, 1),
        };
        var dynamicUniverse = new DynamicUniverse(entryDates);

        var composite = new CompositeTimedUniverseSelector(new IUniverseSelector[] { aumFilter, dynamicUniverse });
        var candidates = new List<Asset> { s_vti, s_tlt, s_gld };

        // As of 2011-01-01: AUM filter keeps VTI, TLT; DynamicUniverse keeps only VTI (TLT not yet)
        var result = composite.SelectAsOf(candidates, new DateOnly(2011, 1, 1));
        result.Should().ContainSingle().Which.Should().Be(s_vti);
    }

    [Fact]
    public void SelectAsOf_AllPass_ShouldReturnAll()
    {
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_tlt] = new DateOnly(2010, 1, 1),
        };
        var dynamicUniverse = new DynamicUniverse(entryDates);
        var composite = new CompositeTimedUniverseSelector(new IUniverseSelector[] { dynamicUniverse });
        var candidates = new List<Asset> { s_vti, s_tlt };

        var result = composite.SelectAsOf(candidates, new DateOnly(2025, 1, 1));
        result.Should().HaveCount(2);
    }

    // ============================================================
    // Select delegates to SelectAsOf with MaxValue
    // ============================================================

    [Fact]
    public void Select_ShouldDelegateToSelectAsOfWithMaxValue()
    {
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_tlt] = new DateOnly(2030, 1, 1), // far future — would be excluded with earlier date
        };
        var dynamicUniverse = new DynamicUniverse(entryDates);
        var composite = new CompositeTimedUniverseSelector(new IUniverseSelector[] { dynamicUniverse });
        var candidates = new List<Asset> { s_vti, s_tlt };

        // Select() uses DateOnly.MaxValue, so both should pass
        var result = composite.Select(candidates);
        result.Should().HaveCount(2);
    }

    // ============================================================
    // Mixed timed and plain selectors
    // ============================================================

    [Fact]
    public void SelectAsOf_MixedTimedAndPlain_ShouldApplyCorrectly()
    {
        // Plain selector: SupersessionFilter
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = new(s_vti, 1000m, new DateOnly(2010, 1, 1), 5_000_000m, SupersededBy: s_spy),
            [s_spy] = new(s_spy, 2000m, new DateOnly(2010, 1, 1), 10_000_000m),
            [s_tlt] = new(s_tlt, 800m, new DateOnly(2010, 1, 1), 3_000_000m),
        };
        var supersessionFilter = new SupersessionFilter(metadata);

        // Timed selector: DynamicUniverse
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_spy] = new DateOnly(2010, 1, 1),
            [s_tlt] = new DateOnly(2020, 1, 1),
        };
        var dynamicUniverse = new DynamicUniverse(entryDates);

        var composite = new CompositeTimedUniverseSelector(
            new IUniverseSelector[] { supersessionFilter, dynamicUniverse });
        var candidates = new List<Asset> { s_vti, s_spy, s_tlt };

        // As of 2015: SupersessionFilter removes VTI (s_spy present), DynamicUniverse removes TLT (2020)
        var result = composite.SelectAsOf(candidates, new DateOnly(2015, 1, 1));
        result.Should().ContainSingle().Which.Should().Be(s_spy);
    }

    // ============================================================
    // Empty selectors / empty candidates
    // ============================================================

    [Fact]
    public void SelectAsOf_NoSelectors_ShouldReturnAllCandidates()
    {
        var composite = new CompositeTimedUniverseSelector(Array.Empty<IUniverseSelector>());
        var candidates = new List<Asset> { s_vti, s_tlt };

        var result = composite.SelectAsOf(candidates, new DateOnly(2025, 1, 1));
        result.Should().HaveCount(2);
    }

    [Fact]
    public void SelectAsOf_EmptyCandidates_ShouldReturnEmpty()
    {
        var entryDates = new Dictionary<Asset, DateOnly> { [s_vti] = new DateOnly(2010, 1, 1) };
        var dynamicUniverse = new DynamicUniverse(entryDates);
        var composite = new CompositeTimedUniverseSelector(new IUniverseSelector[] { dynamicUniverse });

        var result = composite.SelectAsOf(new List<Asset>(), new DateOnly(2025, 1, 1));
        result.Should().BeEmpty();
    }
}

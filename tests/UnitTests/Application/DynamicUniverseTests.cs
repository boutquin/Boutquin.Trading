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

public sealed class DynamicUniverseTests
{
    private static readonly Asset s_vti = new("VTI");
    private static readonly Asset s_bnd = new("BND");
    private static readonly Asset s_gld = new("GLD");
    private static readonly Asset s_unknown = new("UNKNOWN");

    private static IReadOnlyDictionary<Asset, DateOnly> CreateEntryDates() =>
        new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_bnd] = new DateOnly(2015, 6, 15),
            [s_gld] = new DateOnly(2020, 3, 1),
        };

    // ============================================================
    // DynamicUniverse Tests
    // ============================================================

    [Fact]
    public void SelectAsOf_BeforeAnyEntryDate_ReturnsEmpty()
    {
        var universe = new DynamicUniverse(CreateEntryDates());
        var candidates = new List<Asset> { s_vti, s_bnd, s_gld };

        var result = universe.SelectAsOf(candidates, new DateOnly(2009, 12, 31));

        result.Should().BeEmpty();
    }

    [Fact]
    public void SelectAsOf_AfterAllEntryDates_ReturnsAllCandidates()
    {
        var universe = new DynamicUniverse(CreateEntryDates());
        var candidates = new List<Asset> { s_vti, s_bnd, s_gld };

        var result = universe.SelectAsOf(candidates, new DateOnly(2025, 1, 1));

        result.Should().HaveCount(3);
        result.Should().ContainInOrder(s_vti, s_bnd, s_gld);
    }

    [Fact]
    public void SelectAsOf_MixedDates_ReturnsOnlyEligible()
    {
        // VTI: 2010, BND: 2015, GLD: 2020. Query 2017 → only VTI and BND.
        var universe = new DynamicUniverse(CreateEntryDates());
        var candidates = new List<Asset> { s_vti, s_bnd, s_gld };

        var result = universe.SelectAsOf(candidates, new DateOnly(2017, 1, 1));

        result.Should().HaveCount(2);
        result.Should().Contain(s_vti);
        result.Should().Contain(s_bnd);
        result.Should().NotContain(s_gld);
    }

    [Fact]
    public void SelectAsOf_ExactEntryDate_IncludesAsset()
    {
        var universe = new DynamicUniverse(CreateEntryDates());
        var candidates = new List<Asset> { s_bnd };

        // BND entry date is exactly 2015-06-15
        var result = universe.SelectAsOf(candidates, new DateOnly(2015, 6, 15));

        result.Should().ContainSingle().Which.Should().Be(s_bnd);
    }

    [Fact]
    public void SelectAsOf_UnknownAsset_ExcludedSilently()
    {
        var universe = new DynamicUniverse(CreateEntryDates());
        var candidates = new List<Asset> { s_vti, s_unknown };

        var result = universe.SelectAsOf(candidates, new DateOnly(2025, 1, 1));

        result.Should().ContainSingle().Which.Should().Be(s_vti);
    }

    [Fact]
    public void Select_ReturnsAllEverEligible()
    {
        var universe = new DynamicUniverse(CreateEntryDates());
        var candidates = new List<Asset> { s_vti, s_bnd, s_gld };

        var selectResult = universe.Select(candidates);
        var selectAsOfResult = universe.SelectAsOf(candidates, DateOnly.MaxValue);

        selectResult.Should().BeEquivalentTo(selectAsOfResult, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Constructor_NullEntryDates_ThrowsException()
    {
        var act = () => new DynamicUniverse(null!);

        act.Should().Throw<EmptyOrNullDictionaryException>();
    }

    [Fact]
    public void Constructor_EmptyEntryDates_ThrowsException()
    {
        var act = () => new DynamicUniverse(new Dictionary<Asset, DateOnly>());

        act.Should().Throw<EmptyOrNullDictionaryException>();
    }

    [Fact]
    public void SelectAsOf_PreservesOrderOfCandidates()
    {
        var universe = new DynamicUniverse(CreateEntryDates());
        // Provide candidates in reverse alphabetical order
        var candidates = new List<Asset> { s_gld, s_bnd, s_vti };

        var result = universe.SelectAsOf(candidates, new DateOnly(2025, 1, 1));

        result.Should().ContainInOrder(s_gld, s_bnd, s_vti);
    }

    // ============================================================
    // CompositeTimedUniverseSelector Tests
    // ============================================================

    [Fact]
    public void CompositeTimedSelector_ChainsTimedAndPlainSelectors()
    {
        // DynamicUniverse filters by date, MinAumFilter filters by AUM.
        // GLD has entry date 2020. Query at 2017 → GLD excluded by date.
        // SMALL (s_unknown stand-in) excluded by AUM.
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_bnd] = new DateOnly(2015, 6, 15),
            [s_gld] = new DateOnly(2020, 3, 1),
        };

        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = new AssetMetadata(s_vti, AumMillions: 500m, InceptionDate: new DateOnly(2001, 5, 24), AverageDailyVolume: 5_000_000m),
            [s_bnd] = new AssetMetadata(s_bnd, AumMillions: 10m, InceptionDate: new DateOnly(2007, 4, 10), AverageDailyVolume: 2_000_000m),
            [s_gld] = new AssetMetadata(s_gld, AumMillions: 300m, InceptionDate: new DateOnly(2004, 11, 18), AverageDailyVolume: 3_000_000m),
        };

        var dynamicUniverse = new DynamicUniverse(entryDates);
        var aumFilter = new MinAumFilter(100m, metadata);

        var composite = new CompositeTimedUniverseSelector(new List<IUniverseSelector> { dynamicUniverse, aumFilter });
        var candidates = new List<Asset> { s_vti, s_bnd, s_gld };

        // At 2017: DynamicUniverse passes VTI + BND (GLD not yet eligible).
        // MinAumFilter: VTI (500M ≥ 100M) passes, BND (10M < 100M) fails.
        var result = composite.SelectAsOf(candidates, new DateOnly(2017, 1, 1));

        result.Should().ContainSingle().Which.Should().Be(s_vti);
    }

    [Fact]
    public void CompositeTimedSelector_Select_DelegatesToMaxDate()
    {
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_vti] = new DateOnly(2010, 1, 1),
            [s_bnd] = new DateOnly(2015, 6, 15),
        };

        var composite = new CompositeTimedUniverseSelector(
            new List<IUniverseSelector> { new DynamicUniverse(entryDates) });

        var candidates = new List<Asset> { s_vti, s_bnd };

        var selectResult = composite.Select(candidates);
        var selectAsOfResult = composite.SelectAsOf(candidates, DateOnly.MaxValue);

        selectResult.Should().BeEquivalentTo(selectAsOfResult, options => options.WithStrictOrdering());
    }

    // ============================================================
    // SupersessionFilter Tests
    // ============================================================

    private static readonly Asset s_schd = new("SCHD");
    private static readonly Asset s_jepi = new("JEPI");

    [Fact]
    public void SupersessionFilter_ReplacementPresent_ExcludesPredecessor()
    {
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_schd] = new AssetMetadata(s_schd, 50_000m, new DateOnly(2011, 10, 20), 3_000_000m, SupersededBy: s_jepi),
            [s_jepi] = new AssetMetadata(s_jepi, 30_000m, new DateOnly(2020, 5, 20), 5_000_000m),
        };

        var filter = new SupersessionFilter(metadata);
        var result = filter.Select(new List<Asset> { s_schd, s_jepi });

        result.Should().ContainSingle().Which.Should().Be(s_jepi);
    }

    [Fact]
    public void SupersessionFilter_ReplacementAbsent_KeepsPredecessor()
    {
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_schd] = new AssetMetadata(s_schd, 50_000m, new DateOnly(2011, 10, 20), 3_000_000m, SupersededBy: s_jepi),
            [s_jepi] = new AssetMetadata(s_jepi, 30_000m, new DateOnly(2020, 5, 20), 5_000_000m),
        };

        var filter = new SupersessionFilter(metadata);
        // JEPI not in candidate list → SCHD survives
        var result = filter.Select(new List<Asset> { s_schd });

        result.Should().ContainSingle().Which.Should().Be(s_schd);
    }

    [Fact]
    public void SupersessionFilter_NoSupersessionRule_KeepsAll()
    {
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = new AssetMetadata(s_vti, 300_000m, new DateOnly(2001, 5, 24), 5_000_000m),
            [s_bnd] = new AssetMetadata(s_bnd, 100_000m, new DateOnly(2007, 4, 10), 2_000_000m),
        };

        var filter = new SupersessionFilter(metadata);
        var result = filter.Select(new List<Asset> { s_vti, s_bnd });

        result.Should().HaveCount(2);
    }

    [Fact]
    public void SupersessionFilter_UnknownAsset_KeepsIt()
    {
        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_vti] = new AssetMetadata(s_vti, 300_000m, new DateOnly(2001, 5, 24), 5_000_000m),
        };

        var filter = new SupersessionFilter(metadata);
        // s_unknown has no metadata → no supersession rule → kept
        var result = filter.Select(new List<Asset> { s_vti, s_unknown });

        result.Should().HaveCount(2);
    }

    [Fact]
    public void SupersessionFilter_WithDynamicUniverse_TimeAwareExclusion()
    {
        // SCHD available from 2011, JEPI from 2020. SCHD superseded by JEPI.
        // Before 2020: only SCHD eligible → kept (replacement not yet in universe).
        // After 2020: both eligible → SCHD excluded.
        var entryDates = new Dictionary<Asset, DateOnly>
        {
            [s_schd] = new DateOnly(2011, 10, 20),
            [s_jepi] = new DateOnly(2020, 5, 20),
            [s_vti] = new DateOnly(2001, 5, 24),
        };

        var metadata = new Dictionary<Asset, AssetMetadata>
        {
            [s_schd] = new AssetMetadata(s_schd, 50_000m, new DateOnly(2011, 10, 20), 3_000_000m, SupersededBy: s_jepi),
            [s_jepi] = new AssetMetadata(s_jepi, 30_000m, new DateOnly(2020, 5, 20), 5_000_000m),
            [s_vti] = new AssetMetadata(s_vti, 300_000m, new DateOnly(2001, 5, 24), 5_000_000m),
        };

        var composite = new CompositeTimedUniverseSelector(new List<IUniverseSelector>
        {
            new DynamicUniverse(entryDates),
            new SupersessionFilter(metadata),
        });

        var candidates = new List<Asset> { s_vti, s_schd, s_jepi };

        // 2015: JEPI not yet eligible → SCHD kept
        var before = composite.SelectAsOf(candidates, new DateOnly(2015, 1, 1));
        before.Should().HaveCount(2);
        before.Should().Contain(s_vti);
        before.Should().Contain(s_schd);

        // 2021: JEPI eligible → SCHD excluded
        var after = composite.SelectAsOf(candidates, new DateOnly(2021, 1, 1));
        after.Should().HaveCount(2);
        after.Should().Contain(s_vti);
        after.Should().Contain(s_jepi);
        after.Should().NotContain(s_schd);
    }

    [Fact]
    public void SupersessionFilter_NullMetadata_Throws()
    {
        var act = () => new SupersessionFilter(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

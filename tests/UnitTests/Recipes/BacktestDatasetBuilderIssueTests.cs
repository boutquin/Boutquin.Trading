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

using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Diagnostics;
using Boutquin.MarketData.Abstractions.Provenance;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.Trading.Recipes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Recipes;

/// <summary>
/// Tests for <see cref="BacktestDatasetBuilder"/>'s issue propagation logic,
/// verifying that MISSING_VALUES are suppressed, UNEXPECTED_GAP are forwarded,
/// and other issue codes pass through unchanged.
/// </summary>
public sealed class BacktestDatasetBuilderIssueTests
{
    private static BacktestDatasetSpec CreateMinimalSpec(string econSeriesId) =>
        new()
        {
            Symbols = [],
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 1, 31),
            EconomicSeriesIds = [econSeriesId],
        };

    private static DataEnvelope<IReadOnlyList<ScalarObservation>> CreateEconEnvelope(
        IReadOnlyList<DataIssue> issues)
    {
        var obs = new List<ScalarObservation>
        {
            new(new DateOnly(2025, 1, 2), 4.25m, "percent"),
        };

        return new DataEnvelope<IReadOnlyList<ScalarObservation>>(
            obs,
            new DataCoverage(20, 1, 19, 0.05m),
            issues,
            [new DataProvenance(new ProviderCode("TestProvider"), "DGS10", LicenseType.Free, RetrievalMode.Fixture, FreshnessClass.Live, DateTimeOffset.UtcNow, null)]);
    }

    [Fact]
    public async Task PropagateIssues_UnexpectedGap_IsForwarded()
    {
        // Arrange
        var gapIssue = new DataIssue(IssueCode.UnexpectedGap, IssueSeverity.Warning, "Missing data on 2025-01-06 (business day)");
        var envelope = CreateEconEnvelope([gapIssue]);

        var mockPipeline = new Mock<IDataPipeline>();
        mockPipeline
            .Setup(p => p.ExecuteAsync<EconomicSeriesRequest, ScalarObservation>(
                It.IsAny<EconomicSeriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);

        var builder = new BacktestDatasetBuilder(
            mockPipeline.Object,
            NullLogger<BacktestDatasetBuilder>.Instance);

        // Act
        var dataset = await builder.BuildAsync(CreateMinimalSpec("DGS10"));

        // Assert
        dataset.Issues.Should().ContainSingle(i => i.Code == IssueCode.UnexpectedGap,
            "UNEXPECTED_GAP issues from calendar-aware adapters must be propagated");
    }

    [Fact]
    public async Task PropagateIssues_MissingValues_IsFiltered()
    {
        // Arrange
        var missingIssue = new DataIssue(new IssueCode("MISSING_VALUES"), IssueSeverity.Warning, "3 missing values on weekends");
        var envelope = CreateEconEnvelope([missingIssue]);

        var mockPipeline = new Mock<IDataPipeline>();
        mockPipeline
            .Setup(p => p.ExecuteAsync<EconomicSeriesRequest, ScalarObservation>(
                It.IsAny<EconomicSeriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);

        var builder = new BacktestDatasetBuilder(
            mockPipeline.Object,
            NullLogger<BacktestDatasetBuilder>.Instance);

        // Act
        var dataset = await builder.BuildAsync(CreateMinimalSpec("DGS10"));

        // Assert
        dataset.Issues.Should().NotContain(i => i.Code == new IssueCode("MISSING_VALUES"),
            "MISSING_VALUES issues are expected noise and must be suppressed");
    }

    [Fact]
    public async Task PropagateIssues_OtherCodes_ArePassedThrough()
    {
        // Arrange
        var otherIssue = new DataIssue(IssueCode.StaleData, IssueSeverity.Warning, "Last observation is 3 days old");
        var envelope = CreateEconEnvelope([otherIssue]);

        var mockPipeline = new Mock<IDataPipeline>();
        mockPipeline
            .Setup(p => p.ExecuteAsync<EconomicSeriesRequest, ScalarObservation>(
                It.IsAny<EconomicSeriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);

        var builder = new BacktestDatasetBuilder(
            mockPipeline.Object,
            NullLogger<BacktestDatasetBuilder>.Instance);

        // Act
        var dataset = await builder.BuildAsync(CreateMinimalSpec("DGS10"));

        // Assert
        dataset.Issues.Should().ContainSingle(i => i.Code == IssueCode.StaleData,
            "Non-MISSING_VALUES, non-UNEXPECTED_GAP issues must pass through unchanged");
    }

    [Fact]
    public async Task PropagateIssues_MixedCodes_CorrectlyFiltersAndForwards()
    {
        // Arrange — three issues: one of each type
        var issues = new List<DataIssue>
        {
            new(new IssueCode("MISSING_VALUES"), IssueSeverity.Warning, "Weekend gaps"),
            new(IssueCode.UnexpectedGap, IssueSeverity.Warning, "Gap on 2025-01-06"),
            new(IssueCode.StaleData, IssueSeverity.Warning, "Old data"),
        };

        var envelope = CreateEconEnvelope(issues);

        var mockPipeline = new Mock<IDataPipeline>();
        mockPipeline
            .Setup(p => p.ExecuteAsync<EconomicSeriesRequest, ScalarObservation>(
                It.IsAny<EconomicSeriesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope);

        var builder = new BacktestDatasetBuilder(
            mockPipeline.Object,
            NullLogger<BacktestDatasetBuilder>.Instance);

        // Act
        var dataset = await builder.BuildAsync(CreateMinimalSpec("DGS10"));

        // Assert
        dataset.Issues.Should().HaveCount(2);
        dataset.Issues.Should().Contain(i => i.Code == IssueCode.UnexpectedGap);
        dataset.Issues.Should().Contain(i => i.Code == IssueCode.StaleData);
        dataset.Issues.Should().NotContain(i => i.Code == new IssueCode("MISSING_VALUES"));
    }
}

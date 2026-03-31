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

using Boutquin.Trading.Application.Caching;

namespace Boutquin.Trading.Tests.UnitTests.Application.Caching;

/// <summary>
/// Tests for CachingFactorDataFetcher L1 memory cache decorator.
/// </summary>
public sealed class CachingFactorDataFetcherTests
{
    private static readonly DateOnly s_day1 = new(2024, 1, 15);
    private static readonly DateOnly s_day2 = new(2024, 1, 16);
    private static readonly DateOnly s_day3 = new(2024, 1, 17);

    private static List<KeyValuePair<DateOnly, IReadOnlyDictionary<string, decimal>>> CreateFactorData()
    {
        return
        [
            new(s_day1, new Dictionary<string, decimal> { ["Mkt-RF"] = 0.5m, ["SMB"] = 0.1m, ["HML"] = -0.2m }),
            new(s_day2, new Dictionary<string, decimal> { ["Mkt-RF"] = 0.3m, ["SMB"] = -0.1m, ["HML"] = 0.1m }),
            new(s_day3, new Dictionary<string, decimal> { ["Mkt-RF"] = 0.7m, ["SMB"] = 0.2m, ["HML"] = -0.3m })
        ];
    }

    /// <summary>
    /// Second call with same dataset returns cached data — daily.
    /// </summary>
    [Fact]
    public async Task FetchDailyAsync_CacheHit_InnerCalledOnce()
    {
        // Arrange
        var inner = new Mock<IFactorDataFetcher>();
        inner.Setup(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new CachingFactorDataFetcher(inner.Object);

        // Act
        var result1 = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();
        var result2 = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        // Assert
        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
        inner.Verify(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Second call with same dataset returns cached data — monthly.
    /// </summary>
    [Fact]
    public async Task FetchMonthlyAsync_CacheHit_InnerCalledOnce()
    {
        // Arrange
        var inner = new Mock<IFactorDataFetcher>();
        inner.Setup(f => f.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new CachingFactorDataFetcher(inner.Object);

        // Act
        var result1 = await sut.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();
        var result2 = await sut.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        // Assert
        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
        inner.Verify(f => f.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Daily and monthly caches are separate — same dataset in different frequency = two inner calls.
    /// </summary>
    [Fact]
    public async Task DailyAndMonthly_SameDataset_SeparateCaches()
    {
        // Arrange
        var inner = new Mock<IFactorDataFetcher>();
        inner.Setup(f => f.FetchDailyAsync(It.IsAny<FamaFrenchDataset>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());
        inner.Setup(f => f.FetchMonthlyAsync(It.IsAny<FamaFrenchDataset>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new CachingFactorDataFetcher(inner.Object);

        // Act
        await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();
        await sut.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        // Assert — each called once (different caches)
        inner.Verify(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(f => f.FetchMonthlyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Superset filter: fetch full range, then request subset returns filtered data without calling inner.
    /// </summary>
    [Fact]
    public async Task FetchDailyAsync_SupersetFilter_ReturnsFilteredWithoutCallingInner()
    {
        // Arrange
        var inner = new Mock<IFactorDataFetcher>();
        inner.Setup(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        using var sut = new CachingFactorDataFetcher(inner.Object);

        // Act — full range
        await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();
        // Act — subset
        var subset = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, s_day1, s_day2).ToListAsync();

        // Assert
        subset.Should().HaveCount(2);
        inner.Verify(f => f.FetchDailyAsync(It.IsAny<FamaFrenchDataset>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Concurrent calls with same args should materialize exactly once.
    /// </summary>
    [Fact]
    public async Task FetchDailyAsync_ConcurrentCalls_InnerCalledOnce()
    {
        // Arrange
        var callCount = 0;
        var inner = new Mock<IFactorDataFetcher>();
        inner.Setup(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                return CreateFactorData().ToAsyncEnumerable();
            });

        using var sut = new CachingFactorDataFetcher(inner.Object);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync().AsTask())
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert
        callCount.Should().Be(1);
    }

    /// <summary>
    /// Dispose clears cache and disposes inner if disposable.
    /// </summary>
    [Fact]
    public async Task Dispose_ClearsCacheAndDisposesInner()
    {
        // Arrange
        var inner = new Mock<IFactorDataFetcher>();
        var disposableInner = inner.As<IDisposable>();
        inner.Setup(f => f.FetchDailyAsync(It.IsAny<FamaFrenchDataset>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFactorData().ToAsyncEnumerable());

        var sut = new CachingFactorDataFetcher(inner.Object);
        await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        // Act
        sut.Dispose();

        // Assert
        disposableInner.Verify(d => d.Dispose(), Times.Once);
    }

    /// <summary>
    /// Constructor throws on null inner fetcher.
    /// </summary>
    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new CachingFactorDataFetcher(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A faulted fetch should be evicted so the next caller retries.
    /// </summary>
    [Fact]
    public async Task FetchDailyAsync_FaultedFetch_EvictedFromCache()
    {
        // Arrange
        var callCount = 0;
        var inner = new Mock<IFactorDataFetcher>();
        inner.Setup(f => f.FetchDailyAsync(FamaFrenchDataset.ThreeFactors, null, null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("API down");
                }

                return CreateFactorData().ToAsyncEnumerable();
            });

        using var sut = new CachingFactorDataFetcher(inner.Object);

        // Act — first call faults
        var act1 = async () => await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();
        await act1.Should().ThrowAsync<InvalidOperationException>();

        // Act — second call should retry (not return cached faulted task)
        var result = await sut.FetchDailyAsync(FamaFrenchDataset.ThreeFactors).ToListAsync();

        // Assert
        result.Should().HaveCount(3);
        callCount.Should().Be(2);
    }
}

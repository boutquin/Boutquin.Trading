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
/// Tests for CachingEconomicDataFetcher L1 memory cache decorator.
/// </summary>
public sealed class CachingEconomicDataFetcherTests
{
    private static readonly DateOnly s_day1 = new(2024, 1, 15);
    private static readonly DateOnly s_day2 = new(2024, 1, 16);
    private static readonly DateOnly s_day3 = new(2024, 1, 17);

    private static List<KeyValuePair<DateOnly, decimal>> CreateSeriesData()
    {
        return
        [
            new(s_day1, 4.25m),
            new(s_day2, 4.30m),
            new(s_day3, 4.28m)
        ];
    }

    /// <summary>
    /// Second call with same seriesId and date range returns cached data.
    /// </summary>
    [Fact]
    public async Task FetchSeriesAsync_CacheHit_InnerCalledOnce()
    {
        // Arrange
        var inner = new Mock<IEconomicDataFetcher>();
        inner.Setup(f => f.FetchSeriesAsync("DGS10", null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateSeriesData().ToAsyncEnumerable());

        using var sut = new CachingEconomicDataFetcher(inner.Object);

        // Act
        var result1 = await sut.FetchSeriesAsync("DGS10").ToListAsync();
        var result2 = await sut.FetchSeriesAsync("DGS10").ToListAsync();

        // Assert
        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
        inner.Verify(f => f.FetchSeriesAsync("DGS10", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Different seriesId results in cache miss.
    /// </summary>
    [Fact]
    public async Task FetchSeriesAsync_DifferentSeriesId_InnerCalledTwice()
    {
        // Arrange
        var inner = new Mock<IEconomicDataFetcher>();
        inner.Setup(f => f.FetchSeriesAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSeriesData().ToAsyncEnumerable());

        using var sut = new CachingEconomicDataFetcher(inner.Object);

        // Act
        await sut.FetchSeriesAsync("DGS10").ToListAsync();
        await sut.FetchSeriesAsync("DGS3MO").ToListAsync();

        // Assert
        inner.Verify(f => f.FetchSeriesAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Superset filter: fetch full range, then request subset returns filtered data without calling inner.
    /// </summary>
    [Fact]
    public async Task FetchSeriesAsync_SupersetFilter_ReturnsFilteredDataWithoutCallingInner()
    {
        // Arrange
        var inner = new Mock<IEconomicDataFetcher>();
        inner.Setup(f => f.FetchSeriesAsync("DGS10", null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateSeriesData().ToAsyncEnumerable());

        using var sut = new CachingEconomicDataFetcher(inner.Object);

        // Act — first call fetches full range
        await sut.FetchSeriesAsync("DGS10").ToListAsync();
        // Act — second call with subset date range
        var subset = await sut.FetchSeriesAsync("DGS10", s_day1, s_day2).ToListAsync();

        // Assert — subset should be filtered
        subset.Should().HaveCount(2);
        subset.Should().OnlyContain(kvp => kvp.Key >= s_day1 && kvp.Key <= s_day2);
        // Inner should only be called once (for the full range)
        inner.Verify(f => f.FetchSeriesAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Concurrent calls with same args should materialize exactly once.
    /// </summary>
    [Fact]
    public async Task FetchSeriesAsync_ConcurrentCalls_InnerCalledOnce()
    {
        // Arrange
        var callCount = 0;
        var inner = new Mock<IEconomicDataFetcher>();
        inner.Setup(f => f.FetchSeriesAsync("DGS10", null, null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                return CreateSeriesData().ToAsyncEnumerable();
            });

        using var sut = new CachingEconomicDataFetcher(inner.Object);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.FetchSeriesAsync("DGS10").ToListAsync().AsTask())
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
        var inner = new Mock<IEconomicDataFetcher>();
        var disposableInner = inner.As<IDisposable>();
        inner.Setup(f => f.FetchSeriesAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSeriesData().ToAsyncEnumerable());

        var sut = new CachingEconomicDataFetcher(inner.Object);
        await sut.FetchSeriesAsync("DGS10").ToListAsync();

        // Act
        sut.Dispose();

        // Assert
        disposableInner.Verify(d => d.Dispose(), Times.Once);
    }

    /// <summary>
    /// CancellationToken is propagated to inner fetcher.
    /// </summary>
    [Fact]
    public async Task FetchSeriesAsync_CancellationToken_PropagatedToInner()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var inner = new Mock<IEconomicDataFetcher>();
        inner.Setup(f => f.FetchSeriesAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, DateOnly? _, DateOnly? _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return CreateSeriesData().ToAsyncEnumerable();
            });

        using var sut = new CachingEconomicDataFetcher(inner.Object);

        // Act & Assert
        var act = async () => await sut.FetchSeriesAsync("DGS10", cancellationToken: cts.Token).ToListAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Constructor throws on null inner fetcher.
    /// </summary>
    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new CachingEconomicDataFetcher(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

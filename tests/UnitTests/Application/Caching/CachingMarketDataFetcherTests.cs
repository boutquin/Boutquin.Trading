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
/// Tests for CachingMarketDataFetcher L1 memory cache decorator.
/// </summary>
public sealed class CachingMarketDataFetcherTests
{
    private static readonly Asset s_aapl = new("AAPL");
    private static readonly Asset s_msft = new("MSFT");
    private static readonly DateOnly s_day1 = new(2024, 1, 15);
    private static readonly DateOnly s_day2 = new(2024, 1, 16);

    private static MarketData CreateMarketData(DateOnly date, decimal close = 150m) =>
        new(date, 100m, 200m, 50m, close, close, 1_000_000, 0m, 1m);

    private static List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>> CreateMarketDataList()
    {
        return
        [
            new(s_day1, new SortedDictionary<Asset, MarketData> { [s_aapl] = CreateMarketData(s_day1, 150m) }),
            new(s_day2, new SortedDictionary<Asset, MarketData> { [s_aapl] = CreateMarketData(s_day2, 155m) })
        ];
    }

    private static List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>> CreateFxRatesList()
    {
        return
        [
            new(s_day1, new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.EUR] = 0.85m }),
            new(s_day2, new SortedDictionary<CurrencyCode, decimal> { [CurrencyCode.EUR] = 0.86m })
        ];
    }

    /// <summary>
    /// Second call with same symbols returns cached data without calling inner fetcher again.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_CacheHit_InnerCalledOnce()
    {
        // Arrange
        var inner = new Mock<IMarketDataFetcher>();
        var data = CreateMarketDataList();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(data.ToAsyncEnumerable());

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act — first call
        var result1 = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();
        // Act — second call with same args
        var result2 = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();

        // Assert
        result1.Should().HaveCount(2);
        result2.Should().HaveCount(2);
        result1[0].Value[s_aapl].Close.Should().Be(result2[0].Value[s_aapl].Close);
        inner.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Calls with different symbols result in separate inner fetcher calls.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_CacheMiss_InnerCalledForEach()
    {
        // Arrange
        var inner = new Mock<IMarketDataFetcher>();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateMarketDataList().ToAsyncEnumerable());

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act
        await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();
        await sut.FetchMarketDataAsync([s_msft], CancellationToken.None).ToListAsync();

        // Assert — different cache keys, so inner called twice
        inner.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// FX rates are cached independently from market data.
    /// </summary>
    [Fact]
    public async Task FetchFxRatesAsync_CacheHit_InnerCalledOnce()
    {
        // Arrange
        var inner = new Mock<IMarketDataFetcher>();
        var fxData = CreateFxRatesList();
        inner.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(fxData.ToAsyncEnumerable());

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act
        var result1 = await sut.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None).ToListAsync();
        var result2 = await sut.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None).ToListAsync();

        // Assert
        result1.Should().HaveCount(2);
        result2.Should().HaveCount(2);
        inner.Verify(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Parallel calls with same args should materialize exactly once (Lazy pattern).
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_ConcurrentCalls_InnerCalledOnce()
    {
        // Arrange
        var callCount = 0;
        var inner = new Mock<IMarketDataFetcher>();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                return CreateMarketDataList().ToAsyncEnumerable();
            });

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act — launch parallel calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync().AsTask())
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert — should call inner exactly once due to Lazy<Task<...>>
        callCount.Should().Be(1);
    }

    /// <summary>
    /// Dispose clears cache and disposes inner if disposable.
    /// </summary>
    [Fact]
    public async Task Dispose_ClearsCacheAndDisposesInner()
    {
        // Arrange
        var inner = new Mock<IMarketDataFetcher>();
        var disposableInner = inner.As<IDisposable>();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateMarketDataList().ToAsyncEnumerable());

        var sut = new CachingMarketDataFetcher(inner.Object);
        await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();

        // Act
        sut.Dispose();

        // Assert — inner.Dispose was called
        disposableInner.Verify(d => d.Dispose(), Times.Once);
    }

    /// <summary>
    /// CancellationToken is propagated to inner fetcher.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_CancellationToken_PropagatedToInner()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var inner = new Mock<IMarketDataFetcher>();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<Asset> _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return CreateMarketDataList().ToAsyncEnumerable();
            });

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act & Assert
        var act = async () => await sut.FetchMarketDataAsync([s_aapl], cts.Token).ToListAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Constructor throws on null inner fetcher.
    /// </summary>
    [Fact]
    public void Constructor_NullInner_ThrowsArgumentNullException()
    {
        var act = () => new CachingMarketDataFetcher(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A faulted fetch should be evicted so the next caller retries.
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_FaultedFetch_EvictedFromCache()
    {
        // Arrange
        var callCount = 0;
        var inner = new Mock<IMarketDataFetcher>();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("API down");
                }

                return CreateMarketDataList().ToAsyncEnumerable();
            });

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act — first call faults
        var act1 = async () => await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();
        await act1.Should().ThrowAsync<InvalidOperationException>();

        // Act — second call should retry (not return cached faulted task)
        var result = await sut.FetchMarketDataAsync([s_aapl], CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        callCount.Should().Be(2);
    }

    /// <summary>
    /// Non-replayable IEnumerable should work correctly (materialized before key + fetch).
    /// </summary>
    [Fact]
    public async Task FetchMarketDataAsync_NonReplayableEnumerable_WorksCorrectly()
    {
        // Arrange
        var inner = new Mock<IMarketDataFetcher>();
        inner.Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(CreateMarketDataList().ToAsyncEnumerable());

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act — pass a one-shot IEnumerable (yield-based)
        static IEnumerable<Asset> OneShotEnumerable()
        {
            yield return s_aapl;
        }

        var result = await sut.FetchMarketDataAsync(OneShotEnumerable(), CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    /// <summary>
    /// A faulted FX fetch should be evicted so the next caller retries.
    /// </summary>
    [Fact]
    public async Task FetchFxRatesAsync_FaultedFetch_EvictedFromCache()
    {
        // Arrange
        var callCount = 0;
        var inner = new Mock<IMarketDataFetcher>();
        inner.Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("API down");
                }

                return CreateFxRatesList().ToAsyncEnumerable();
            });

        using var sut = new CachingMarketDataFetcher(inner.Object);

        // Act — first call faults
        var act1 = async () => await sut.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None).ToListAsync();
        await act1.Should().ThrowAsync<InvalidOperationException>();

        // Act — second call should retry
        var result = await sut.FetchFxRatesAsync(["USD_EUR"], CancellationToken.None).ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        callCount.Should().Be(2);
    }
}

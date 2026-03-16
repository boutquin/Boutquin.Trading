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

namespace Boutquin.Trading.Tests.UnitTests.Data;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}

public sealed class CompositeMarketDataFetcherTests
{
    [Fact]
    public async Task DelegatesToEquityFetcher()
    {
        var expectedData = new KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>(
            new DateOnly(2024, 1, 15),
            new SortedDictionary<Asset, MarketData>
            {
                [new Asset("AAPL")] = new MarketData(
                    new DateOnly(2024, 1, 15), 185.09m, 186.42m, 183.55m, 185.92m, 185.62m, 44234500, 0.24m, 1.0m)
            });

        var mockEquity = new Mock<IMarketDataFetcher>();
        mockEquity
            .Setup(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { expectedData }.ToAsyncEnumerable());

        var mockFx = new Mock<IMarketDataFetcher>();

        var composite = new CompositeMarketDataFetcher(mockEquity.Object, mockFx.Object);

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>();
        await foreach (var item in composite.FetchMarketDataAsync(new[] { new Asset("AAPL") }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(1);
        results[0].Key.Should().Be(new DateOnly(2024, 1, 15));
        mockEquity.Verify(f => f.FetchMarketDataAsync(It.IsAny<IEnumerable<Asset>>()), Times.Once);
    }

    [Fact]
    public async Task DelegatesToFxFetcher()
    {
        var expectedData = new KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>(
            new DateOnly(2024, 1, 10),
            new SortedDictionary<CurrencyCode, decimal>
            {
                [CurrencyCode.EUR] = 0.91358m
            });

        var mockEquity = new Mock<IMarketDataFetcher>();

        var mockFx = new Mock<IMarketDataFetcher>();
        mockFx
            .Setup(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { expectedData }.ToAsyncEnumerable());

        var composite = new CompositeMarketDataFetcher(mockEquity.Object, mockFx.Object);

        var results = new List<KeyValuePair<DateOnly, SortedDictionary<CurrencyCode, decimal>>>();
        await foreach (var item in composite.FetchFxRatesAsync(new[] { "USD_EUR" }, CancellationToken.None))
        {
            results.Add(item);
        }

        results.Should().HaveCount(1);
        results[0].Value[CurrencyCode.EUR].Should().Be(0.91358m);
        mockFx.Verify(f => f.FetchFxRatesAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesChildren()
    {
        var mockEquity = new Mock<IMarketDataFetcher>();
        var disposableEquity = mockEquity.As<IDisposable>();

        var mockFx = new Mock<IMarketDataFetcher>();
        var disposableFx = mockFx.As<IDisposable>();

        var composite = new CompositeMarketDataFetcher(mockEquity.Object, mockFx.Object);
        composite.Dispose();

        disposableEquity.Verify(d => d.Dispose(), Times.Once);
        disposableFx.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public void Constructor_NullEquityFetcher_ThrowsArgumentNullException()
    {
        var mockFx = new Mock<IMarketDataFetcher>();

        var act = () => new CompositeMarketDataFetcher(null!, mockFx.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("equityFetcher");
    }

    [Fact]
    public void Constructor_NullFxFetcher_ThrowsArgumentNullException()
    {
        var mockEquity = new Mock<IMarketDataFetcher>();

        var act = () => new CompositeMarketDataFetcher(mockEquity.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fxFetcher");
    }
}

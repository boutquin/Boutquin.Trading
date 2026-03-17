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

namespace Boutquin.Trading.Tests.UnitTests.Domain;

/// <summary>
/// Tests for <see cref="MarketDataProcessor"/> (R2I-10).
/// </summary>
public sealed class MarketDataProcessorTests
{
    private static MarketData CreateMarketData(DateOnly date, decimal close = 100m) =>
        new(date, 100m, 105m, 95m, close, close, 1000000L, 0m, 1m);

    [Fact]
    public async Task ProcessAndStoreMarketDataAsync_FetchesAndStoresData()
    {
        var asset = new Asset("AAPL");
        var date = new DateOnly(2024, 1, 15);
        var md = CreateMarketData(date);

        var dataPoints = new List<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>
        {
            new(date, new SortedDictionary<Asset, MarketData> { [asset] = md })
        };

        var fetcherMock = new Mock<IMarketDataFetcher>();
        fetcherMock.Setup(f => f.FetchMarketDataAsync(
                It.IsAny<IEnumerable<Asset>>(), It.IsAny<CancellationToken>()))
            .Returns(dataPoints.ToAsyncEnumerable());

        var storageMock = new Mock<IMarketDataStorage>();

        var processor = new MarketDataProcessor(fetcherMock.Object, storageMock.Object);
        await processor.ProcessAndStoreMarketDataAsync([asset], CancellationToken.None);

        storageMock.Verify(s => s.SaveMarketDataAsync(
            It.Is<KeyValuePair<DateOnly, SortedDictionary<Asset, MarketData>>>(kv => kv.Key == date),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAndStoreMarketDataAsync_NullSymbols_ThrowsArgumentNullException()
    {
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var storageMock = new Mock<IMarketDataStorage>();
        var processor = new MarketDataProcessor(fetcherMock.Object, storageMock.Object);

        var act = () => processor.ProcessAndStoreMarketDataAsync(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessAndStoreMarketDataAsync_EmptySymbols_ThrowsArgumentException()
    {
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var storageMock = new Mock<IMarketDataStorage>();
        var processor = new MarketDataProcessor(fetcherMock.Object, storageMock.Object);

        var act = () => processor.ProcessAndStoreMarketDataAsync([], CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessAndStoreMarketDataAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var storageMock = new Mock<IMarketDataStorage>();
        var processor = new MarketDataProcessor(fetcherMock.Object, storageMock.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => processor.ProcessAndStoreMarketDataAsync([new Asset("AAPL")], cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullFetcher_ThrowsArgumentNullException()
    {
        var storageMock = new Mock<IMarketDataStorage>();
        var act = () => new MarketDataProcessor(null!, storageMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullStorage_ThrowsArgumentNullException()
    {
        var fetcherMock = new Mock<IMarketDataFetcher>();
        var act = () => new MarketDataProcessor(fetcherMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

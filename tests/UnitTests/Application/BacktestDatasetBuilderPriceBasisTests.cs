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
using Boutquin.MarketData.Abstractions.Exceptions;
using Boutquin.MarketData.Abstractions.Provenance;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.MarketData.Abstractions.Snapshots;
using Boutquin.Trading.Recipes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Trading.Tests.UnitTests.Application;

public sealed class BacktestDatasetBuilderPriceBasisTests
{
    private static readonly PriceBasis s_total = new(PriceAdjustment.All, AdjustedCloseSource.ProviderSupplied, PriceAdjustment.None);
    private static readonly PriceBasis s_other = new(PriceAdjustment.All, AdjustedCloseSource.AdapterComputed, PriceAdjustment.Splits);
    private static readonly DateOnly s_date = new(2026, 1, 15);

    [Fact]
    public async Task CarriesMappedBasisAndVenue()
    {
        var pipeline = new RecordingPipeline { Provenance = [Stamp(s_total)] };
        var spec = Spec() with { SymbolExchanges = new Dictionary<string, ExchangeCode> { ["XYZ"] = ExchangeCode.XNYS } };
        var result = await Builder(pipeline).BuildAsync(spec);
        pipeline.Requests.Should().ContainSingle();
        pipeline.Requests[0].Basis.Should().Be(s_total);
        pipeline.Requests[0].Exchange.Should().Be(ExchangeCode.XNYS);
        result.Prices.Should().ContainKey(s_date);
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task RejectsEveryMissingSymbolBeforeAnyFetch()
    {
        foreach (var map in new IReadOnlyDictionary<string, PriceBasis>[]
                 { new Dictionary<string, PriceBasis>(), new Dictionary<string, PriceBasis> { ["XYZ"] = s_total } })
        {
            var pipeline = new RecordingPipeline();
            var spec = Spec() with { Symbols = [new Symbol("XYZ"), new Symbol("ZZZ"), new Symbol("AAA")], SymbolPriceBases = map };
            var act = () => Builder(pipeline).BuildAsync(spec);
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*AAA*ZZZ*");
            pipeline.Requests.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task RejectsNonTotalReturnDeliveryOutsideFetchCatch()
    {
        var pipeline = new RecordingPipeline { Provenance = [Stamp(PriceBasis.Raw)] };
        var act = () => Builder(pipeline).BuildAsync(Spec());
        var thrown = await act.Should().ThrowAsync<PriceBasisMismatchException>();
        thrown.Which.Requested.Should().Be(s_total);
        thrown.Which.Stored.Should().Be(PriceBasis.Raw);
        thrown.Which.Provider.Should().Be("test");
    }

    [Fact]
    public async Task PropagatesPipelineUnsupportedBasis()
    {
        var expected = new PriceBasisNotSupportedException(s_total, [s_other]);
        var pipeline = new RecordingPipeline { Failure = expected };
        var act = () => Builder(pipeline).BuildAsync(Spec());
        var thrown = await act.Should().ThrowAsync<PriceBasisNotSupportedException>();
        thrown.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task RetainsUnstampedBarsWithExplicitWarning()
    {
        var pipeline = new RecordingPipeline();
        var result = await Builder(pipeline).BuildAsync(Spec());
        result.Prices.Should().ContainKey(s_date);
        result.Issues.Should().ContainSingle(i => i.Code.Value == "PRICE_BASIS_UNPROVEN");
        result.Issues.Should().NotContain(i => i.Code.Value == "PRICE_FETCH_FAILED");
    }

    [Fact]
    public async Task OrdinaryProviderExhaustionRemainsFetchIssue()
    {
        var pipeline = new RecordingPipeline { Failure = new InvalidOperationException("All adapters failed.") };
        var result = await Builder(pipeline).BuildAsync(Spec());
        result.Issues.Should().ContainSingle(i => i.Code.Value == "PRICE_FETCH_FAILED");
    }

    [Fact]
    public async Task PropagatesTypedPipelineMismatchWithoutSkippingSymbol()
    {
        var expected = new PriceBasisMismatchException(s_total, s_other, "test");
        var pipeline = new RecordingPipeline { Failure = expected };
        var act = () => Builder(pipeline).BuildAsync(Spec());
        var thrown = await act.Should().ThrowAsync<PriceBasisMismatchException>();
        thrown.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task RejectsNonTotalReturnRequestBeforeAnyFetch()
    {
        var pipeline = new RecordingPipeline();
        var spec = Spec() with { SymbolPriceBases = new Dictionary<string, PriceBasis> { ["XYZ"] = PriceBasis.Raw } };
        var act = () => Builder(pipeline).BuildAsync(spec);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*XYZ*");
        pipeline.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task EmptySymbolsNeedNoBasisMap()
    {
        var pipeline = new RecordingPipeline();
        var spec = Spec() with { Symbols = [], SymbolPriceBases = new Dictionary<string, PriceBasis>() };
        var result = await Builder(pipeline).BuildAsync(spec);
        result.Prices.Should().BeEmpty();
        pipeline.Requests.Should().BeEmpty();
    }

    private static BacktestDatasetBuilder Builder(RecordingPipeline pipeline) =>
        new(pipeline, NullLogger<BacktestDatasetBuilder>.Instance);

    private static BacktestDatasetSpec Spec() => new()
    {
        Symbols = [new Symbol("XYZ")],
        StartDate = s_date,
        EndDate = s_date,
        SymbolPriceBases = new Dictionary<string, PriceBasis> { ["XYZ"] = s_total },
    };

    private static DataProvenance Stamp(PriceBasis basis) =>
        new(new ProviderCode("test"), "fixture", default, default, default, DateTimeOffset.UnixEpoch, null) { PriceBasis = basis };

    private sealed class RecordingPipeline : IDataPipeline
    {
        public List<PriceHistoryRequest> Requests { get; } = [];
        public IReadOnlyList<DataProvenance> Provenance { get; init; } = [];
        public Exception? Failure { get; init; }

        public Task<DataEnvelope<IReadOnlyList<TRecord>>> ExecuteAsync<TRequest, TRecord>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IDataRequest
        {
            Requests.Add((PriceHistoryRequest)(object)request);
            if (Failure is not null)
            {
                return Task.FromException<DataEnvelope<IReadOnlyList<TRecord>>>(Failure);
            }
            IReadOnlyList<Bar> bars = [new(s_date, 100m, 100m, 100m, 100m, 100m, 1)];
            return Task.FromResult(new DataEnvelope<IReadOnlyList<TRecord>>((IReadOnlyList<TRecord>)bars,
                new DataCoverage(1, 1, 0, 1m), [], Provenance));
        }

        public Task<IReadOnlyDictionary<TDatasetKey, DataEnvelope<IReadOnlyList<TRecord>>>> ExecutePartitionedAsync<TRequest, TRecord, TPartitionKey, TDatasetKey>(
            TRequest request, Func<TRequest, TPartitionKey, TRequest> splitForPartition, Func<TRequest, TPartitionKey, TDatasetKey> buildDatasetKey,
            CancellationToken cancellationToken = default) where TRequest : IBatchDataRequest<TPartitionKey> where TDatasetKey : notnull => throw new NotSupportedException();

        public Task<IDataSnapshot> AcquireReadSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

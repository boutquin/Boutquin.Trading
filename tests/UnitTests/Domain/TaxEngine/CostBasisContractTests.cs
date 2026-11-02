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

using Boutquin.Domain.Extensions;
using Boutquin.Trading.Domain.TaxEngine;

namespace Boutquin.Trading.Tests.UnitTests.Domain.TaxEngine;

public sealed class CostBasisContractTests
{
    [Fact]
    public void RecordSale_ExposesOldAndIdentificationAwareOverloads()
    {
        var arities = typeof(ICostBasisMethod)
            .GetMethods()
            .Where(method => method.Name == nameof(ICostBasisMethod.RecordSale))
            .Select(method => method.GetParameters().Length)
            .Order()
            .ToArray();

        arities.Should().Equal(4, 5);
    }

    [Fact]
    public void RecordSale_WithNoIdentification_DelegatesToExistingImplementation()
    {
        ICostBasisMethod method = new ExistingCostBasisMethod();

        var result = method.RecordSale(new DateOnly(2026, 9, 16), 2m, 12m, 1m, null);

        result.Should().BeSameAs(ExistingCostBasisMethod.ExpectedDisposals);
    }

    [Fact]
    public void RecordSale_WithMismatchedIdentifiedQuantity_Throws()
    {
        ICostBasisMethod method = new ExistingCostBasisMethod();
        var identifications = new[]
        {
            new LotIdentification(new DateOnly(2025, 1, 2), Currency.USD.Amount(10m), 1m),
        };

        var act = () => method.RecordSale(
            new DateOnly(2026, 9, 16), 2m, 12m, 1m, identifications);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("identifiedLots")
            .WithMessage("*sum*1*sale quantity*2*");
    }

    [Fact]
    public void RecordSale_WithValidIdentification_OnExistingImplementer_FailsClosed()
    {
        ICostBasisMethod method = new ExistingCostBasisMethod();
        var identifications = new[]
        {
            new LotIdentification(new DateOnly(2025, 1, 2), Currency.USD.Amount(10m), 2m),
        };

        var act = () => method.RecordSale(
            new DateOnly(2026, 9, 16), 2m, 12m, 1m, identifications);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*identification*");
    }

    private sealed class ExistingCostBasisMethod : ICostBasisMethod
    {
        internal static IReadOnlyList<LotDisposal> ExpectedDisposals { get; } =
            Array.Empty<LotDisposal>();

        public decimal TotalCostBasis => 0m;

        public decimal TotalQuantity => 0m;

        public decimal CostPerShare => 0m;

        public void RecordPurchase(DateOnly date, decimal quantity, decimal pricePerShare, decimal commission)
        {
        }

        public IReadOnlyList<LotDisposal> RecordSale(
            DateOnly date,
            decimal quantity,
            decimal pricePerShare,
            decimal commission) => ExpectedDisposals;

        public RocAdjustmentResult AdjustForReturnOfCapital(DateOnly date, decimal rocPerShare) =>
            new(Currency.USD.Amount(0m), Currency.USD.Amount(0m));
    }
}

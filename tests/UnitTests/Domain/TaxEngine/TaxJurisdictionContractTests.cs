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

public sealed class TaxJurisdictionContractTests
{
    [Fact]
    public void ComputeTaxImpact_ExposesCompatibilityAndIncomeAwareOverloads()
    {
        var arities = typeof(ITaxJurisdiction)
            .GetMethods()
            .Where(method => method.Name == nameof(ITaxJurisdiction.ComputeTaxImpact))
            .Select(method => method.GetParameters().Length)
            .Order()
            .ToArray();

        arities.Should().Equal(3, 5);
    }

    [Fact]
    public void FilingStatus_HasExactlyTheSourcedMembers()
    {
        Enum.GetNames<FilingStatus>().Should().Equal(
            nameof(FilingStatus.Single),
            nameof(FilingStatus.MarriedFilingJointly),
            nameof(FilingStatus.MarriedFilingSeparately),
            nameof(FilingStatus.HeadOfHousehold));
    }

    [Fact]
    public void IncomeAwareOverload_OnExistingImplementer_FailsClosed()
    {
        ITaxJurisdiction jurisdiction = new ExistingTaxJurisdiction();

        var act = () => jurisdiction.ComputeTaxImpact(
            Array.Empty<LotDisposal>(),
            0.30m,
            2025,
            100_000m,
            FilingStatus.Single);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*taxable-income*");
    }

    [Fact]
    public void CompatibilityOverload_StillDispatchesToExistingImplementation()
    {
        ITaxJurisdiction jurisdiction = new ExistingTaxJurisdiction();

        var result = jurisdiction.ComputeTaxImpact(Array.Empty<LotDisposal>(), 0.30m, 2025);

        result.Should().BeSameAs(ExistingTaxJurisdiction.ExpectedImpact);
    }

    private sealed class ExistingTaxJurisdiction : ITaxJurisdiction
    {
        internal static TaxImpact ExpectedImpact { get; } = new(
            Currency.USD.Amount(0m),
            Currency.USD.Amount(0m),
            Currency.USD.Amount(0m),
            Currency.USD.Amount(0m),
            Currency.USD.Amount(0m));

        public TaxImpact ComputeTaxImpact(
            IReadOnlyList<LotDisposal> disposals,
            decimal marginalTaxRate,
            int taxYear) => ExpectedImpact;

        public DividendTaxImpact ComputeDividendTax(
            IReadOnlyList<DividendRecord> dividends,
            decimal quantity,
            decimal marginalTaxRate) => null!;

        public ICostBasisMethod CreateCostBasisMethod() => null!;

        public ILossHarvestingRule CreateLossHarvestingRule() => null!;
    }
}

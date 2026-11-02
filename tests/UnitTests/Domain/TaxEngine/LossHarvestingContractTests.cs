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

using System.Text.Json;
using Boutquin.Domain.Extensions;
using Boutquin.Trading.Domain.TaxEngine;

namespace Boutquin.Trading.Tests.UnitTests.Domain.TaxEngine;

public sealed class LossHarvestingContractTests
{
    [Fact]
    public void ExistingFourFieldJson_RemainsDeserializable()
    {
        var json = JsonSerializer.Serialize(new
        {
            IsLossDisallowed = true,
            DisallowedAmount = Currency.USD.Amount(10m),
            AllowedAmount = Currency.USD.Amount(2m),
            Rule = "WashSale",
        });

        var result = JsonSerializer.Deserialize<LossHarvestingResult>(json);

        result.Should().Be(new LossHarvestingResult(
            true, Currency.USD.Amount(10m), Currency.USD.Amount(2m), "WashSale"));
    }

    [Theory]
    [InlineData(DisallowedLossDisposition.AddedToReplacementBasis)]
    [InlineData(DisallowedLossDisposition.PermanentlyLost)]
    public void JsonRoundTrip_PreservesExplicitDisposition(DisallowedLossDisposition disposition)
    {
        var expected = new LossHarvestingResult(
            true, Currency.USD.Amount(10m), Currency.USD.Amount(2m), "WashSale", disposition);

        var actual = JsonSerializer.Deserialize<LossHarvestingResult>(JsonSerializer.Serialize(expected));

        actual.Should().Be(expected);
    }

    [Fact]
    public void DisallowedLossDisposition_HasExactlyTheSourcedMembers()
    {
        Enum.GetNames<DisallowedLossDisposition>()
            .Should().Equal(nameof(DisallowedLossDisposition.AddedToReplacementBasis),
                nameof(DisallowedLossDisposition.PermanentlyLost));
    }

    [Fact]
    public void ExistingConstructorAndDeconstruction_RetainFourFieldShape()
    {
        var result = new LossHarvestingResult(
            true,
            Currency.USD.Amount(10m),
            Currency.USD.Amount(0m),
            "WashSale");

        var (isDisallowed, disallowedAmount, allowedAmount, rule) = result;

        isDisallowed.Should().BeTrue();
        disallowedAmount.Should().Be(Currency.USD.Amount(10m));
        allowedAmount.Should().Be(Currency.USD.Amount(0m));
        rule.Should().Be("WashSale");
        result.Disposition.Should().Be(DisallowedLossDisposition.AddedToReplacementBasis);
    }

    [Fact]
    public void SecondaryConstructor_SetsPermanentLossDisposition()
    {
        var result = new LossHarvestingResult(
            true,
            Currency.USD.Amount(10m),
            Currency.USD.Amount(0m),
            "WashSale",
            DisallowedLossDisposition.PermanentlyLost);

        result.Disposition.Should().Be(DisallowedLossDisposition.PermanentlyLost);
    }

    [Fact]
    public void WithExpression_CanChangeDisposition()
    {
        var result = new LossHarvestingResult(
            true,
            Currency.USD.Amount(10m),
            Currency.USD.Amount(0m),
            "WashSale");

        var permanentLoss = result with
        {
            Disposition = DisallowedLossDisposition.PermanentlyLost,
        };

        permanentLoss.Disposition.Should().Be(DisallowedLossDisposition.PermanentlyLost);
    }
}

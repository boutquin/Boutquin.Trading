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

namespace Boutquin.Trading.Tests.UnitTests.Application;

using Boutquin.Trading.Application.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies that the nested <see cref="CovarianceEstimatorParameters"/> surface binds from
/// <c>IConfiguration</c> through <c>IOptions&lt;BacktestOptions&gt;</c> (M-1).
/// </summary>
public sealed class CovarianceEstimatorParametersBindingTests
{
    private static BacktestOptions Bind(Dictionary<string, string?> configData)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<BacktestOptions>(configuration.GetSection(BacktestOptions.SectionName));

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<BacktestOptions>>().Value;
    }

    [Fact]
    public void CovarianceParameters_BindsEverySetProperty_LeavesUnsetNull()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = "DccNl",
            ["Backtest:CovarianceParameters:EwmaLambda"] = "0.97",
            ["Backtest:CovarianceParameters:DccNlEstimateParameters"] = "false",
            ["Backtest:CovarianceParameters:DccNlAlpha"] = "0.05",
            ["Backtest:CovarianceParameters:FactorGraphicalLassoNumFactors"] = "3",
            ["Backtest:CovarianceParameters:PoetThresholdMultiplier"] = "0.25",
        });

        var p = options.CovarianceParameters;
        p.Should().NotBeNull();
        p.EwmaLambda.Should().Be(0.97m);
        p.DccNlEstimateParameters.Should().BeFalse();
        p.DccNlAlpha.Should().Be(0.05m);
        p.FactorGraphicalLassoNumFactors.Should().Be(3);
        p.PoetThresholdMultiplier.Should().Be(0.25);

        // Every property left unset binds to null (defer-to-estimator-default semantics).
        p.DccNlBeta.Should().BeNull();
        p.DenoisedApplyLedoitWolfShrinkage.Should().BeNull();
        p.DetoningAlpha.Should().BeNull();
        p.DoublySparseEigenvectorThreshold.Should().BeNull();
        p.GerberThreshold.Should().BeNull();
        p.NercomeSplitFraction.Should().BeNull();
        p.PoetNumFactors.Should().BeNull();
    }

    [Fact]
    public void CovarianceParameters_AbsentSection_YieldsNonNullAllNull()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Backtest:CovarianceEstimator"] = "EWMA",
        });

        var p = options.CovarianceParameters;
        p.Should().NotBeNull("the property is pre-initialized to new()");
        p.EwmaLambda.Should().BeNull();
        p.DccNlAlpha.Should().BeNull();
        p.DccNlBeta.Should().BeNull();
        p.DccNlEstimateParameters.Should().BeNull();
        p.DenoisedApplyLedoitWolfShrinkage.Should().BeNull();
        p.DetoningAlpha.Should().BeNull();
        p.DoublySparseEigenvectorThreshold.Should().BeNull();
        p.FactorGraphicalLassoNumFactors.Should().BeNull();
        p.GerberThreshold.Should().BeNull();
        p.NercomeSplitFraction.Should().BeNull();
        p.PoetNumFactors.Should().BeNull();
        p.PoetThresholdMultiplier.Should().BeNull();
    }
}

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

namespace Boutquin.Trading.Tests.UnitTests.Application.Calendar;

using Boutquin.Trading.Application.Calendar;
using Boutquin.Trading.Application.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests for trading calendar DI registration and CalendarOptions.
/// </summary>
public sealed class CalendarDiTests
{
    [Fact]
    public void CalendarOptions_DefaultValues_UsCalendar()
    {
        var options = new CalendarOptions();

        options.TradingCalendar.Should().Be("US");
        options.CompositionMode.Should().Be("All");
        options.Calendars.Should().BeEmpty();
    }

    [Fact]
    public void CalendarOptions_SectionName_IsCalendar()
    {
        CalendarOptions.SectionName.Should().Be("Calendar");
    }

    [Fact]
    public void AddBoutquinTrading_UsConfig_RegistersUsTradingCalendar()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = "US",
        });

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<UsTradingCalendar>();
        calendar.TradingDaysPerYear.Should().Be(252);
    }

    [Fact]
    public void AddBoutquinTrading_CanadianConfig_RegistersCanadianTradingCalendar()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = "Canadian",
        });

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<CanadianTradingCalendar>();
        calendar.TradingDaysPerYear.Should().Be(250);
    }

    [Fact]
    public void AddBoutquinTrading_CompositeConfig_RegistersComposite()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = "Composite",
            ["Calendar:CompositionMode"] = "Any",
            ["Calendar:Calendars:0"] = "US",
            ["Calendar:Calendars:1"] = "Canadian",
        });

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<CompositeTradingCalendar>();
        // Any mode → max of 252, 250
        calendar.TradingDaysPerYear.Should().Be(252);
    }

    [Fact]
    public void AddBoutquinTrading_NoCalendarConfig_DefaultsToUs()
    {
        // Empty config — CalendarOptions defaults apply
        var config = BuildConfig(new Dictionary<string, string?>());

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<UsTradingCalendar>();
    }

    [Fact]
    public void AddBoutquinTrading_UnknownCalendar_ThrowsArgumentOutOfRange()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = "London",
        });

        var sp = BuildServiceProvider(config);

        var act = sp.GetRequiredService<ITradingCalendar>;

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Unknown TradingCalendar*");
    }

    [Fact]
    public void AddBoutquinTrading_CompositeEmptyCalendars_ThrowsArgumentException()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = "Composite",
        });

        var sp = BuildServiceProvider(config);

        var act = sp.GetRequiredService<ITradingCalendar>;

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one constituent calendar*");
    }

    // --- Fix #6 TYPE-01: Case-insensitive config matching ---

    [Theory]
    [InlineData("us")]
    [InlineData("Us")]
    [InlineData(" US ")]
    [InlineData("US")]
    public void AddBoutquinTrading_CaseInsensitiveCalendar_ResolvesCorrectly(string value)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = value,
        });

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<UsTradingCalendar>();
    }

    [Theory]
    [InlineData("canadian")]
    [InlineData("CANADIAN")]
    [InlineData(" Canadian ")]
    public void AddBoutquinTrading_CaseInsensitiveCanadian_ResolvesCorrectly(string value)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = value,
        });

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<CanadianTradingCalendar>();
    }

    [Theory]
    [InlineData("any")]
    [InlineData("ANY")]
    [InlineData(" Any ")]
    public void AddBoutquinTrading_CaseInsensitiveCompositionMode_ResolvesCorrectly(string mode)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Calendar:TradingCalendar"] = "Composite",
            ["Calendar:CompositionMode"] = mode,
            ["Calendar:Calendars:0"] = "US",
        });

        var sp = BuildServiceProvider(config);
        var calendar = sp.GetRequiredService<ITradingCalendar>();

        calendar.Should().BeOfType<CompositeTradingCalendar>();
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration config)
    {
        // AddBoutquinTrading registers all services. We provide baseline config for
        // non-calendar services so their factories don't throw when resolved during
        // DI validation. Calendar tests only resolve ITradingCalendar.
        var baselineSettings = new Dictionary<string, string?>
        {
            ["CostModel:TransactionCostType"] = "PercentageOfValue",
            ["CostModel:CommissionRate"] = "0.001",
            ["CostModel:SlippageType"] = "NoSlippage",
            ["Backtest:ConstructionModel"] = "EqualWeight",
        };

        // Merge caller settings on top of baseline
        var merged = new Dictionary<string, string?>(baselineSettings);
        foreach (var section in config.AsEnumerable())
        {
            if (section.Value is not null)
            {
                merged[section.Key] = section.Value;
            }
        }

        var mergedConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(merged)
            .Build();

        var services = new ServiceCollection();
        services.AddBoutquinTrading(mergedConfig);
        return services.BuildServiceProvider();
    }
}

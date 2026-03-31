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
using Boutquin.Trading.Application.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boutquin.Trading.Tests.UnitTests.Application.Caching;

/// <summary>
/// Tests for DI cache decorator registration via ServiceCollectionExtensions.
/// </summary>
public sealed class CacheDecoratorRegistrationTests : IDisposable
{
    private readonly string _tempDir;

    public CacheDecoratorRegistrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cache_di_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void L1Enabled_L2Disabled_ResolvedFetcherIsCachingMarketDataFetcher()
    {
        var config = BuildConfig(enableMemoryCache: true, dataDirectory: null);
        var services = new ServiceCollection();
        var mockFetcher = new Mock<IMarketDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IMarketDataFetcher>();

        resolved.Should().BeOfType<CachingMarketDataFetcher>();
    }

    [Fact]
    public void L1Disabled_L2Disabled_ResolvedFetcherIsBaseInstance()
    {
        var config = BuildConfig(enableMemoryCache: false, dataDirectory: null);
        var services = new ServiceCollection();
        var mockFetcher = new Mock<IMarketDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IMarketDataFetcher>();

        // No decorators — should be the original mock proxy
        resolved.Should().NotBeOfType<CachingMarketDataFetcher>();
        resolved.Should().NotBeOfType<WriteThroughMarketDataFetcher>();
    }

    [Fact]
    public void L1Enabled_L2Enabled_ResolvedFetcherIsCachingWrappingWriteThrough()
    {
        var config = BuildConfig(enableMemoryCache: true, dataDirectory: _tempDir);
        var services = new ServiceCollection();
        var mockFetcher = new Mock<IMarketDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IMarketDataFetcher>();

        // Outer: L1 CachingMarketDataFetcher wrapping L2 WriteThroughMarketDataFetcher
        resolved.Should().BeOfType<CachingMarketDataFetcher>();
    }

    [Fact]
    public void L1Disabled_L2Enabled_ResolvedFetcherIsWriteThrough()
    {
        var config = BuildConfig(enableMemoryCache: false, dataDirectory: _tempDir);
        var services = new ServiceCollection();
        var mockFetcher = new Mock<IMarketDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IMarketDataFetcher>();

        resolved.Should().BeOfType<WriteThroughMarketDataFetcher>();
    }

    [Fact]
    public void EconomicDataFetcher_L1Enabled_ResolvedIsCachingDecorator()
    {
        var config = BuildConfig(enableMemoryCache: true, dataDirectory: null);
        var services = new ServiceCollection();
        var mockFetcher = new Mock<IEconomicDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IEconomicDataFetcher>();

        resolved.Should().BeOfType<CachingEconomicDataFetcher>();
    }

    [Fact]
    public void FactorDataFetcher_L1AndL2Enabled_ResolvedIsCachingDecorator()
    {
        var config = BuildConfig(enableMemoryCache: true, dataDirectory: _tempDir);
        var services = new ServiceCollection();
        var mockFetcher = new Mock<IFactorDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IFactorDataFetcher>();

        resolved.Should().BeOfType<CachingFactorDataFetcher>();
    }

    [Fact]
    public void NoCacheSection_DefaultsApply_L1OnlyRegistered()
    {
        // Empty config — no "Cache" section. CacheOptions defaults: EnableMemoryCache=true, DataDirectory=null
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        var mockFetcher = new Mock<IMarketDataFetcher>();
        services.AddSingleton(mockFetcher.Object);
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IMarketDataFetcher>();

        // Default: L1 enabled, L2 disabled
        resolved.Should().BeOfType<CachingMarketDataFetcher>();
    }

    [Fact]
    public void NoBaseFetcherRegistered_DoesNotRegisterDecorator()
    {
        var config = BuildConfig(enableMemoryCache: true, dataDirectory: null);
        var services = new ServiceCollection();
        // Intentionally NOT registering a base IMarketDataFetcher
        services.AddBoutquinTradingCaching(config);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetService<IMarketDataFetcher>();

        // Should be null — no base fetcher, no decorator
        resolved.Should().BeNull();
    }

    private static IConfiguration BuildConfig(bool enableMemoryCache, string? dataDirectory)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Cache:EnableMemoryCache"] = enableMemoryCache.ToString(),
        };

        if (dataDirectory != null)
        {
            dict["Cache:DataDirectory"] = dataDirectory;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }
}

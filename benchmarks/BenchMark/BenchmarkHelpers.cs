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

namespace Boutquin.Trading.BenchMark;

using Domain.Events;

/// <summary>
/// Shared helpers for constructing synthetic datasets and backtest infrastructure.
/// </summary>
internal static class BenchmarkHelpers
{
    /// <summary>
    /// Builds a deterministic synthetic dataset using GBM price paths.
    /// </summary>
    internal static FakeBacktestDataset BuildDataset(
        IReadOnlyList<Symbol> assets,
        DateOnly startDate,
        int tradingDays,
        decimal[] startPrices,
        double[] annualVols,
        double[]? annualDrifts = null,
        int seed = 42)
    {
        var rng = new Random(seed);
        var prices = new SortedDictionary<DateOnly, SortedDictionary<Symbol, Bar>>();
        decimal[] current = startPrices.ToArray();
        var date = startDate;

        for (int day = 0; day < tradingDays; day++)
        {
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
            }

            var dayBars = new SortedDictionary<Symbol, Bar>();
            for (int i = 0; i < assets.Count; i++)
            {
                double drift = (annualDrifts?[i] ?? 0.0) / 252.0;
                double sigma = annualVols[i] / Math.Sqrt(252);
                decimal ret = (decimal)(drift + sigma * rng.NextGaussian());
                decimal close = Math.Max(0.01m, current[i] * (1m + ret));
                decimal open = Math.Max(0.01m, current[i] * (1m + (decimal)(sigma * rng.NextGaussian() * 0.3)));
                decimal high = Math.Max(open, close) * (1m + Math.Abs((decimal)(sigma * rng.NextGaussian() * 0.2)));
                decimal low = Math.Min(open, close) * Math.Max(0.001m, 1m - Math.Abs((decimal)(sigma * rng.NextGaussian() * 0.2)));
                dayBars[assets[i]] = new Bar(date, open, high, low, close, close, 5_000_000L);
                current[i] = close;
            }

            prices[date] = dayBars;
            date = date.AddDays(1);
        }

        return new FakeBacktestDataset { Prices = prices };
    }

    /// <summary>
    /// Generates N arrays of T independent daily return observations (IID normal, zero correlation).
    /// </summary>
    internal static decimal[][] GenerateReturns(int n, int t, double annualVol = 0.15, int seed = 42)
    {
        var rng = new Random(seed);
        double dailySigma = annualVol / Math.Sqrt(252);
        return [.. Enumerable.Range(0, n).Select(_ =>
            Enumerable.Range(0, t).Select(__ => (decimal)(rng.NextGaussian() * dailySigma)).ToArray())];
    }

    internal static IReadOnlyDictionary<Type, IEventHandler> BuildHandlers() =>
        new Dictionary<Type, IEventHandler>
        {
            { typeof(OrderEvent),  new OrderEventHandler() },
            { typeof(MarketEvent), new MarketEventHandler() },
            { typeof(FillEvent),   new FillEventHandler() },
            { typeof(SignalEvent), new SignalEventHandler() },
        };

    internal static Portfolio BuildPortfolio(
        CurrencyCode baseCurrency,
        IStrategy strategy,
        IReadOnlyDictionary<Symbol, CurrencyCode> assetCurrencies,
        IReadOnlyDictionary<Type, IEventHandler> handlers,
        IBrokerage broker) =>
        new(baseCurrency,
            new ReadOnlyDictionary<string, IStrategy>(
                new Dictionary<string, IStrategy> { { strategy.Name, strategy } }),
            assetCurrencies,
            handlers,
            broker);
}

internal static class RandomExtensions
{
    internal static double NextGaussian(this Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}

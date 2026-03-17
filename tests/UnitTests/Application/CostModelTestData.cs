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

public sealed class CostModelTestData
{
    /// <summary>
    /// Test data for PercentageOfValueCostModel: rate, fillPrice, quantity, tradeAction, expected.
    /// Commission = fillPrice * quantity * rate.
    /// </summary>
    public static IEnumerable<object[]> PercentageOfValueData =>
    [
        // 0.1% rate, $100, 100 shares, Buy → 100*100*0.001 = 10
        [0.001m, 100m, 100, TradeAction.Buy, 10.00m],
        // 0.1% rate, $50, 200 shares, Sell → 50*200*0.001 = 10
        [0.001m, 50m, 200, TradeAction.Sell, 10.00m],
        // 0.5% rate, $200, 50 shares, Buy → 200*50*0.005 = 50
        [0.005m, 200m, 50, TradeAction.Buy, 50.00m]
    ];

    /// <summary>
    /// Test data for FixedPerTradeCostModel: fixedAmount, fillPrice, quantity, tradeAction, expected.
    /// Commission = fixedAmount regardless of trade.
    /// </summary>
    public static IEnumerable<object[]> FixedPerTradeData =>
    [
        [9.99m, 100m, 100, TradeAction.Buy, 9.99m],
        [4.95m, 500m, 50, TradeAction.Sell, 4.95m],
        [1.00m, 10m, 1000, TradeAction.Buy, 1.00m]
    ];

    /// <summary>
    /// Test data for PerShareCostModel: perShareRate, fillPrice, quantity, tradeAction, expected.
    /// Commission = quantity * perShareRate.
    /// </summary>
    public static IEnumerable<object[]> PerShareData =>
    [
        // $0.005/share, 100 shares → 0.50
        [0.005m, 100m, 100, TradeAction.Buy, 0.50m],
        // $0.01/share, 500 shares → 5.00
        [0.01m, 200m, 500, TradeAction.Sell, 5.00m],
        // $0.001/share, 10000 shares → 10.00
        [0.001m, 50m, 10000, TradeAction.Buy, 10.00m]
    ];

    /// <summary>
    /// Test data for TieredCostModel: tiers, fillPrice, quantity, tradeAction, expected.
    /// </summary>
    public static IEnumerable<object[]> TieredData =>
    [
        // Tier: <=10000 → 0.1%, >10000 → 0.05%. Trade=100*50=5000 → 5000*0.001=5
        [
            (IReadOnlyList<(decimal, decimal)>)new List<(decimal, decimal)>
            {
                (10000m, 0.001m),
                (decimal.MaxValue, 0.0005m)
            },
            100m, 50, TradeAction.Buy, 5.00m
        ],
        // Same tiers. Trade=200*100=20000 → exceeds first tier → 20000*0.0005=10
        [
            (IReadOnlyList<(decimal, decimal)>)new List<(decimal, decimal)>
            {
                (10000m, 0.001m),
                (decimal.MaxValue, 0.0005m)
            },
            200m, 100, TradeAction.Sell, 10.00m
        ],
        // Exactly at boundary: Trade=100*100=10000 → 10000*0.001=10
        [
            (IReadOnlyList<(decimal, decimal)>)new List<(decimal, decimal)>
            {
                (10000m, 0.001m),
                (decimal.MaxValue, 0.0005m)
            },
            100m, 100, TradeAction.Buy, 10.00m
        ]
    ];
}

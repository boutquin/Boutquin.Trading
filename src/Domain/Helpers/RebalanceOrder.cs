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

namespace Boutquin.Trading.Domain.Helpers;

using ValueObjects;

/// <summary>
/// Represents a single rebalance order produced by diffing target weights against current holdings.
/// Quantity is always positive; TradeAction indicates direction.
/// </summary>
/// <param name="Asset">The asset to trade.</param>
/// <param name="TradeAction">Buy or Sell.</param>
/// <param name="Quantity">Number of shares (always positive).</param>
/// <param name="TargetWeight">The target portfolio weight for this asset.</param>
/// <param name="CurrentWeight">The current portfolio weight for this asset.</param>
public sealed record RebalanceOrder(
    Asset Asset,
    TradeAction TradeAction,
    int Quantity,
    decimal TargetWeight,
    decimal CurrentWeight);

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

namespace Boutquin.Trading.Application.SlippageModels;

/// <summary>
/// A slippage model that applies no slippage — returns the theoretical price unchanged.
/// </summary>
public sealed class NoSlippage : ISlippageModel
{
    /// <inheritdoc />
    public decimal CalculateFillPrice(decimal theoreticalPrice, int quantity, TradeAction tradeAction) =>
        theoreticalPrice;
}

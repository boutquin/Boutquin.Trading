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

using Boutquin.Trading.Domain.ValueObjects;

namespace Boutquin.Trading.Domain.Analytics;

/// <summary>
/// Represents the result of a correlation analysis across portfolio assets.
/// </summary>
/// <param name="CorrelationMatrix">The N×N correlation matrix (values between -1 and 1).</param>
/// <param name="AssetNames">The ordered list of asset names corresponding to matrix indices.</param>
/// <param name="DiversificationRatio">The ratio of weighted average volatilities to portfolio volatility.
/// A value of 1.0 means perfectly correlated (no diversification benefit); greater than 1.0 means diversification benefit exists.</param>
public sealed record CorrelationAnalysisResult(
    decimal[,] CorrelationMatrix,
    IReadOnlyList<Asset> AssetNames,
    decimal DiversificationRatio);

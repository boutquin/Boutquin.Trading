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

namespace Boutquin.Trading.Domain.Analytics;

using ValueObjects;

/// <summary>
/// Metadata for a single asset used by universe selectors to evaluate eligibility.
/// </summary>
/// <param name="Asset">The asset ticker.</param>
/// <param name="AumMillions">Assets under management in millions of USD.</param>
/// <param name="InceptionDate">The date the ETF began trading.</param>
/// <param name="AverageDailyVolume">Average daily trading volume (shares).</param>
public sealed record AssetMetadata(
    Asset Asset,
    decimal AumMillions,
    DateOnly InceptionDate,
    decimal AverageDailyVolume);

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

namespace Boutquin.Trading.Domain.ValueObjects;

/// <summary>
/// Holds per-asset weight floors and caps for portfolio construction constraints.
/// </summary>
/// <param name="Floors">Per-asset minimum weights. Null or empty means no floors.</param>
/// <param name="Caps">Per-asset maximum weights. Null or empty means no caps.</param>
public sealed record AssetWeightConstraints(
    IReadOnlyDictionary<Asset, decimal>? Floors = null,
    IReadOnlyDictionary<Asset, decimal>? Caps = null);

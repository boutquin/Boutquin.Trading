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

/// <summary>
/// Decomposes a total return into local-currency and currency components.
/// <c>TotalReturn = (1 + LocalReturn) * (1 + CurrencyReturn) - 1</c>,
/// where <c>CrossTerm = LocalReturn * CurrencyReturn</c>.
/// </summary>
/// <param name="TotalReturn">Total return in the investor's base currency.</param>
/// <param name="LocalReturn">Return in the asset's local currency.</param>
/// <param name="CurrencyReturn">Return attributable to FX movement.</param>
/// <param name="CrossTerm">Interaction term: <c>LocalReturn * CurrencyReturn</c>.</param>
public sealed record CurrencyReturnDecomposition(
    decimal TotalReturn,
    decimal LocalReturn,
    decimal CurrencyReturn,
    decimal CrossTerm);

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

namespace Boutquin.Trading.Domain.TaxEngine;

using Enums;
using ValueObjects;

/// <summary>
/// A historical trade record used by loss harvesting rules
/// to evaluate wash sale / superficial loss windows.
/// </summary>
public sealed record TradeRecord(
    DateOnly Date,
    Asset Asset,
    TradeAction Action,
    decimal Quantity,
    decimal Price);

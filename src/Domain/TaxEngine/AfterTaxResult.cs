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

/// <summary>
/// Result of an after-tax return calculation for a single position in a specific account type.
/// </summary>
/// <param name="PreTaxReturn">The pre-tax return as a decimal (e.g., 0.10 for 10%).</param>
/// <param name="AfterTaxReturn">The after-tax return as a decimal.</param>
/// <param name="TaxDrag">The difference between pre-tax and after-tax return (always non-negative).</param>
/// <param name="EstimatedTax">The estimated total tax amount in dollars.</param>
public sealed record AfterTaxResult(
    decimal PreTaxReturn,
    decimal AfterTaxReturn,
    decimal TaxDrag,
    decimal EstimatedTax);

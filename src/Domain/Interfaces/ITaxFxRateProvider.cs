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

namespace Boutquin.Trading.Domain.Interfaces;
/// <summary>
/// Returns the official FX rate for tax purposes on a given date.
/// Separate from <see cref="ICurrencyConversionService"/> because tax authorities
/// require specific official rates. For current Bank of Canada data, implementations use the
/// indicative daily average rate published once each business day by 16:30 ET.
/// </summary>
/// <remarks>
/// <para>
/// Bank of Canada noon rates are a discontinued legacy series last updated on 2017-04-28 and are
/// valid only for dates on or before that boundary. An implementation supporting both series must
/// expose which series answered through its documented diagnostics or provenance and must not
/// interpolate across the boundary.
/// </para>
/// <para>
/// A foreign-currency disposition requires three separately dated conversions: proceeds at the sale
/// date, adjusted cost basis at the acquisition date, and outlays at the date incurred. Call this
/// provider once per leg; applying one rate to the whole disposition merges foreign-exchange gain or
/// loss into capital gain or loss.
/// </para>
/// <para>
/// Implementations document whether a non-business-day request resolves to the prior business day
/// or throws. Returned rates retain the source precision and are indicative rather than executable.
/// </para>
/// </remarks>
public interface ITaxFxRateProvider
{
    /// <summary>
    /// Returns the official daily average FX rate for one tax calculation leg on a given date.
    /// </summary>
    /// <param name="from">The source currency.</param>
    /// <param name="to">The target currency.</param>
    /// <param name="date">The date for which the official rate is required.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous request.</param>
    /// <returns>
    /// A task that completes with the official rate from <paramref name="from"/> to
    /// <paramref name="to"/> on the resolved source date.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The operation is canceled through <paramref name="cancellationToken"/>.
    /// </exception>
    Task<decimal> GetRateAsync(
        Currency from,
        Currency to,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

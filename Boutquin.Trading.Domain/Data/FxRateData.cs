// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.Domain.Data;

/// <summary>
/// The FxRateData record encapsulates the foreign exchange rate data points
/// for a specific currency pair at a specific point in time, providing
/// information about the rate date, base currency, quote currency, and
/// exchange rate.
/// </summary>
/// <param name="RateDate">The date of the foreign exchange rate data,
/// represented as a DateOnly object.
/// </param>
/// <param name="BaseCurrencyCode">The base currency of the currency pair
/// for which the foreign exchange rate is provided, represented as a
/// CurrencyCode enum value.
/// </param>
/// <param name="QuoteCurrencyCode">The quote currency of the currency pair
/// for which the foreign exchange rate is provided, represented as a
/// CurrencyCode enum value.
/// </param>
/// <param name="Rate">The foreign exchange rate between the base currency
/// and the quote currency, represented as a decimal value.
/// </param>
/// <example>
/// var fxRateData = new FxRateData(
///     DateOnly.Parse("2021-09-01"),
///     CurrencyCode.USD,
///     CurrencyCode.EUR,
///     0.85m);
/// </example>
public record FxRateData(
    DateOnly RateDate,
    CurrencyCode BaseCurrencyCode,
    CurrencyCode QuoteCurrencyCode,
    decimal Rate);

// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
//
namespace Boutquin.Trading.Domain.Interfaces;

/// <summary>
/// The ICurrencyConversionService interface defines the contract for a currency conversion
/// service, which is responsible for converting an amount between two currencies at a specific
/// timestamp.
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>
    /// Converts an amount from one currency to another at a specific timestamp asynchronously.
    /// </summary>
    /// <param name="timestamp">The timestamp at which the conversion should take place.</param>
    /// <param name="amount">The amount to be converted.</param>
    /// <param name="fromCurrency">The currency code of the original currency.</param>
    /// <param name="toCurrency">The currency code of the target currency.</param>
    /// <returns>A task representing the asynchronous operation, with the converted amount as the result.</returns>
    /// <remarks>
    /// The ConvertAsync method should be implemented by a currency conversion service to
    /// perform the currency conversion. The method should take into account the historical
    /// exchange rates at the specified timestamp to perform the conversion.
    /// </remarks>
    /// <example>
    /// This is an example of how the ConvertAsync method can be used:
    /// <code>
    /// ICurrencyConversionService conversionService = new MyCustomCurrencyConversionService();
    /// DateOnly timestamp = DateOnly.Today;
    /// decimal amount = 1000;
    /// CurrencyCode fromCurrency = CurrencyCode.USD;
    /// CurrencyCode toCurrency = CurrencyCode.EUR;
    ///
    /// decimal convertedAmount = await conversionService.ConvertAsync(timestamp, amount, fromCurrency, toCurrency);
    /// Console.WriteLine($"Converted amount: {convertedAmount}");
    /// </code>
    /// </example>
    Task<decimal> ConvertAsync(DateOnly timestamp, decimal amount, CurrencyCode fromCurrency, CurrencyCode toCurrency);
}

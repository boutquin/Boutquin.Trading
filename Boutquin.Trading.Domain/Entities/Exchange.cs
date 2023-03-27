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
/// <summary>
/// Represents an exchange where securities are traded.
/// </summary>
public sealed class Exchange
{
    /// <summary>
    /// Gets the market identifier code.
    /// </summary>
    public ExchangeCode Code { get; }

    /// <summary>
    /// Gets the exchange name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the time zone ID associated with the exchange.
    /// </summary>
    public TimeZoneCode TimeZoneId { get; }

    /// <summary>
    /// Gets the city where the exchange is located.
    /// </summary>
    public string City { get; }

    /// <summary>
    /// Gets the country code associated with the exchange.
    /// </summary>
    public CountryCode CountryCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Exchange"/> class.
    /// </summary>
    /// <param name="code">The market identifier code.</param>
    /// <param name="name">The exchange name. Must not be null or empty.</param>
    /// <param name="timeZoneId">The time zone ID associated with the exchange.</param>
    /// <param name="city">The city where the exchange is located. Must not be null or empty.</param>
    /// <param name="countryCode">The country code associated with the exchange.</param>
    /// <exception cref="ArgumentNullException">Thrown when name or city is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when code, timeZoneId, or countryCode is undefined in their respective enums.</exception>
    public Exchange(
        ExchangeCode code, 
        string name, 
        TimeZoneCode timeZoneId, 
        string city, 
        CountryCode countryCode)
    {
        if (!Enum.IsDefined(typeof(ExchangeCode), code))
        {
            throw new ArgumentException($"Invalid ExchangeCode: {code}", nameof(code));
        }
        
        if (!Enum.IsDefined(typeof(TimeZoneCode), timeZoneId))
        {
            throw new ArgumentException($"Invalid TimeZoneCode: {timeZoneId}", nameof(timeZoneId));
        }
        
        if (!Enum.IsDefined(typeof(CountryCode), countryCode))
        {
            throw new ArgumentException($"Invalid CountryCode: {countryCode}", nameof(countryCode));
        }
        
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name), "Name must not be null or empty.");
        }
        
        if (string.IsNullOrEmpty(city))
        {
            throw new ArgumentNullException(nameof(city), "City must not be null or empty.");
        }

        Code = code;
        Name = name;
        TimeZoneId = timeZoneId;
        City = city;
        CountryCode = countryCode;
    }
}

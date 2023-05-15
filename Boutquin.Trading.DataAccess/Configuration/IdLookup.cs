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

namespace Boutquin.Trading.DataAccess.Configuration;

/// <summary>
/// A static class to look up exchange and city IDs by exchange codes and city names.
/// </summary>
public static class IdLookup
{
    /// <summary>
    /// The dictionary containing the exchange IDs as values and exchange codes as keys.
    /// </summary>
    private static readonly Dictionary<ExchangeCode, int> _exchangeLookup = new()
    {
        { ExchangeCode.XNYS, 1 },
        { ExchangeCode.XNAS, 2 },
        { ExchangeCode.XTSE, 3 },
        { ExchangeCode.XSHG, 4 },
        { ExchangeCode.XHKG, 5 },
        { ExchangeCode.XPAR, 6 },
        { ExchangeCode.XLON, 7 },
        { ExchangeCode.XETR, 8 },
        { ExchangeCode.XMOS, 9 },
        { ExchangeCode.XTOR, 10 }
    };

    /// <summary>
    /// The dictionary containing the city IDs as values and city names as keys.
    /// </summary>
    private static readonly Dictionary<string, int> _cityLookup = new()
    {
        { "New York", 1 },
        { "Tokyo", 2 },
        { "Shanghai", 3 },
        { "Hong Kong", 4 },
        { "Paris", 5 },
        { "London", 6 },
        { "Frankfurt", 7 },
        { "Moscow", 8 },
        { "Toronto", 9 },
    };

    /// <summary>
    /// Gets the exchange ID by exchange code.
    /// </summary>
    /// <param name="exchangeCode">The exchange code.</param>
    /// <returns>The exchange ID.</returns>
    /// <exception cref="ArgumentException">Thrown when the given exchange code is not found in the dictionary.</exception>
    public static int GetExchangeId(ExchangeCode exchangeCode)
    {
        if (_exchangeLookup.TryGetValue(exchangeCode, out var exchangeId))
        {
            return exchangeId;
        }

        throw new ArgumentException($"The given exchange code ({exchangeCode}) is not found in the lookup dictionary.");
    }

    /// <summary>
    /// Gets the city ID by city name.
    /// </summary>
    /// <param name="cityName">The city name.</param>
    /// <returns>The city ID.</returns>
    /// <exception cref="ArgumentException">Thrown when the given city name is not found in the dictionary.</exception>
    public static int GetCityId(string cityName)
    {
        if (_cityLookup.TryGetValue(cityName, out var cityId))
        {
            return cityId;
        }

        throw new ArgumentException($"The given city name ({cityName}) is not found in the lookup dictionary.");
    }
}

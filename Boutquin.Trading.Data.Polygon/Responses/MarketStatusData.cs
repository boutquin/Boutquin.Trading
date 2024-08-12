// Copyright (c) 2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Data.Polygon.Responses
{
    /// <summary>
    /// The MarketStatusData record encapsulates the market status information
    /// provided by the polygon.io API, detailing the status of after hours,
    /// early hours, various exchanges, market status, and the server time.
    /// </summary>
    /// <param name="AfterHours">Indicates whether the market is in after-hours trading, represented as a boolean value.</param>
    /// <param name="Currencies">Represents the status of cryptocurrency and foreign exchange markets, encapsulated in a CurrenciesStatus record.</param>
    /// <param name="EarlyHours">Indicates whether the market is in early-hours trading, represented as a boolean value.</param>
    /// <param name="Exchanges">Represents the status of various stock exchanges, encapsulated in an ExchangesStatus record.</param>
    /// <param name="Market">The overall market status, represented as a string value.</param>
    /// <param name="ServerTime">The server time when the status was recorded, represented as a DateTimeOffset object.</param>
    /// <example>
    /// var marketStatusData = new MarketStatusData(
    ///     true,
    ///     new CurrenciesStatus("open", "open"),
    ///     false,
    ///     new ExchangesStatus("extended-hours", "extended-hours", "closed"),
    ///     "extended-hours",
    ///     DateTimeOffset.Parse("2020-11-10T17:37:37-05:00"));
    /// </example>
    public sealed record MarketStatusData(
        bool AfterHours,
        CurrenciesStatus Currencies,
        bool EarlyHours,
        ExchangesStatus Exchanges,
        string Market,
        DateTimeOffset ServerTime);
}

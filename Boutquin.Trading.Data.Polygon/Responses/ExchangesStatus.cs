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
namespace Boutquin.Trading.Data.Polygon.Responses;

/// <summary>
/// The ExchangesStatus record encapsulates the status of various stock exchanges.
/// </summary>
/// <param name="Nasdaq">The status of the NASDAQ exchange, represented as a string value.</param>
/// <param name="Nyse">The status of the NYSE exchange, represented as a string value.</param>
/// <param name="Otc">The status of the OTC exchange, represented as a string value.</param>
/// <example>
/// var exchangesStatus = new ExchangesStatus("extended-hours", "extended-hours", "closed");
/// </example>
public sealed record ExchangesStatus(
    string Nasdaq,
    string Nyse,
    string Otc);

﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Domain.Helpers;

using Enums;

/// <summary>
/// The Asset record encapsulates the information about a financial asset, including its symbol and currency.
/// This record can be used to uniquely identify an asset and its associated currency within a trading strategy or portfolio.
/// </summary>
/// <param name="Symbol">The unique symbol or ticker of the financial asset, represented as a string.</param>
/// <param name="Currency">The currency in which the asset is denominated, represented as a CurrencyCode enum value.</param>
public record Asset(
    string Symbol,
    CurrencyCode Currency)
{
    /// <summary>
    /// Returns a string representation of the Asset record, including the asset symbol and currency.
    /// </summary>
    /// <returns>A string representation of the Asset record.</returns>
    public override string ToString() => $"{Symbol} ({Currency})";
}

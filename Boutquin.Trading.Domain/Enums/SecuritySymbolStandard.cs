﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.Domain.Enums;

/// <summary>
/// Represents the different standards for security symbols depending on the market and the type of security.
/// </summary>
public enum SecuritySymbolStandard
{
    /// <summary>
    /// Committee on Uniform Security Identification Procedures. Used primarily in the United States for stocks, bonds, and other securities.
    /// </summary>
    [Description("CUSIP")]
    Cusip,

    /// <summary>
    /// International Securities Identification Number. Used globally for stocks, bonds, and other securities.
    /// </summary>
    [Description("ISIN")]
    Isin,

    /// <summary>
    /// Stock Exchange Daily Official List. Used primarily in the United Kingdom for stocks and other securities.
    /// </summary>
    [Description("SEDOL")]
    Sedol,

    /// <summary>
    /// Reuters Instrument Code. Used globally for stocks, bonds, and other securities.
    /// </summary>
    [Description("RIC")]
    Ric,

    /// <summary>
    /// Bloomberg Ticker. Used globally for stocks, bonds, and other securities.
    /// </summary>
    [Description("Bloomberg Ticker")]
    BloombergTicker
}

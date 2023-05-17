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

namespace Boutquin.Trading.Domain.Enums;
using System.ComponentModel;

/// <summary>
/// Represents the ISO 10383 market identifier codes for the major exchanges.
/// </summary>
public enum ExchangeCode
{
    /// <summary>
    /// New York Stock Exchange
    /// </summary>
    [Description("New York Stock Exchange")]
    XNYS,
    /// <summary>
    /// NASDAQ Stock Market
    /// </summary>
    [Description("NASDAQ Stock Market")]
    XNAS,
    /// <summary>
    /// Tokyo Stock Exchange
    /// </summary>
    [Description("Tokyo Stock Exchange")]
    XTSE,
    /// <summary>
    /// Shanghai Stock Exchange
    /// </summary>
    [Description("Shanghai Stock Exchange")]
    XSHG,
    /// <summary>
    /// Hong Kong Stock Exchange
    /// </summary>
    [Description("Hong Kong Stock Exchange")]
    XHKG,
    /// <summary>
    /// Euronext Paris
    /// </summary>
    [Description("Euronext Paris")]
    XPAR,
    /// <summary>
    /// London Stock Exchange
    /// </summary>
    [Description("London Stock Exchange")]
    XLON,
    /// <summary>
    /// Deutsche Boerse XETRA
    /// </summary>
    [Description("Deutsche Boerse XETRA")]
    XETR,
    /// <summary>
    /// Moscow Exchange
    /// </summary>
    [Description("Moscow Exchange")]
    XMOS,
    /// <summary>
    /// Toronto Stock Exchange
    /// </summary>
    [Description("Toronto Stock Exchange")]
    XTOR
}

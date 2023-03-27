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

using System.ComponentModel;

namespace Boutquin.Trading.Domain.Enums;

/// <summary>
/// Represents the ISO 3166-1:2020 alpha-2 country codes.
/// </summary>
/// <summary>
/// Represents the ISO 3166-1:2020 alpha-2 country codes.
/// </summary>
public enum CountryCode
{
    /// <summary>
    /// Canada.
    /// </summary>
    [Description("Canada")]
    CA,

    /// <summary>
    /// China.
    /// </summary>
    [Description("China")]
    CN,

    /// <summary>
    /// France.
    /// </summary>
    [Description("France")]
    FR,

    /// <summary>
    /// Germany.
    /// </summary>
    [Description("Germany")]
    DE,

    /// <summary>
    /// India.
    /// </summary>
    [Description("India")]
    IN,

    /// <summary>
    /// Japan.
    /// </summary>
    [Description("Japan")]
    JP,

    /// <summary>
    /// Russia.
    /// </summary>
    [Description("Russia")]
    RU,

    /// <summary>
    /// South Korea.
    /// </summary>
    [Description("South Korea")]
    KR,

    /// <summary>
    /// United Kingdom.
    /// </summary>
    [Description("United Kingdom")]
    GB,

    /// <summary>
    /// United States.
    /// </summary>
    [Description("United States")]
    US
}

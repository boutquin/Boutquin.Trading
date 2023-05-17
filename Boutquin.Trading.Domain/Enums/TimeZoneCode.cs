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
/// Represents the ISO 8601 Time Zone Codes.
/// </summary>
public enum TimeZoneCode
{
    /// <summary>
    /// Coordinated Universal Time.
    /// </summary>
    [Description("Z")]
    UTC,

    /// <summary>
    /// Central European Time.
    /// </summary>
    [Description("+01:00")]
    CET,

    /// <summary>
    /// Greenwich Mean Time.
    /// </summary>
    [Description("GMT")]
    GMT,

    /// <summary>
    /// Eastern Standard Time.
    /// </summary>
    [Description("-05:00")]
    EST,

    /// <summary>
    /// China Standard Time.
    /// </summary>
    [Description("+08:00")]
    CST,

    /// <summary>
    /// Japan Standard Time.
    /// </summary>
    [Description("+09:00")]
    JST,

    /// <summary>
    /// Hong Kong Time.
    /// </summary>
    [Description("+08:00")]
    HKT,

    /// <summary>
    /// Moscow Standard Time.
    /// </summary>
    [Description("+04:00")]
    MSK,

    /// <summary>
    /// Australian Eastern Standard Time.
    /// </summary>
    [Description("+10:00")]
    AEST,
}

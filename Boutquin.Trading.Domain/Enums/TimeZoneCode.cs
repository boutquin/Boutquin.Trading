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
    /// Central European Summer Time.
    /// </summary>
    [Description("+02:00")]
    CEST,

    /// <summary>
    /// Eastern European Summer Time.
    /// </summary>
    [Description("+03:00")]
    EEST,

    /// <summary>
    /// Moscow Standard Time.
    /// </summary>
    [Description("+04:00")]
    MSD,

    /// <summary>
    /// Pakistan Standard Time.
    /// </summary>
    [Description("+05:00")]
    PKT,

    /// <summary>
    /// Alma-Ata Time.
    /// </summary>
    [Description("+06:00")]
    ALMT,

    /// <summary>
    /// Indochina Time.
    /// </summary>
    [Description("+07:00")]
    ICT,

    /// <summary>
    /// Hong Kong Time.
    /// </summary>
    [Description("+08:00")]
    HKT,

    /// <summary>
    /// Japan Standard Time.
    /// </summary>
    [Description("+09:00")]
    JST,

    /// <summary>
    /// Australian Eastern Standard Time.
    /// </summary>
    [Description("+10:00")]
    AEST,

    /// <summary>
    /// Australian Eastern Daylight Time.
    /// </summary>
    [Description("+11:00")]
    AEDT,

    /// <summary>
    /// New Zealand Daylight Time.
    /// </summary>
    [Description("+12:00")]
    NZDT
}

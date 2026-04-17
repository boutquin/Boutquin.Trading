// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Domain.Enums;

/// <summary>
/// Canadian provinces and territories, used for provincial dividend tax credit rates.
/// </summary>
public enum CanadianProvince
{
    /// <summary>Ontario</summary>
    [Description("Ontario")]
    ON,

    /// <summary>Quebec</summary>
    [Description("Quebec")]
    QC,

    /// <summary>British Columbia</summary>
    [Description("British Columbia")]
    BC,

    /// <summary>Alberta</summary>
    [Description("Alberta")]
    AB,

    /// <summary>Manitoba</summary>
    [Description("Manitoba")]
    MB,

    /// <summary>Saskatchewan</summary>
    [Description("Saskatchewan")]
    SK,

    /// <summary>Nova Scotia</summary>
    [Description("Nova Scotia")]
    NS,

    /// <summary>New Brunswick</summary>
    [Description("New Brunswick")]
    NB,

    /// <summary>Prince Edward Island</summary>
    [Description("Prince Edward Island")]
    PE,

    /// <summary>Newfoundland and Labrador</summary>
    [Description("Newfoundland and Labrador")]
    NL,

    /// <summary>Northwest Territories</summary>
    [Description("Northwest Territories")]
    NT,

    /// <summary>Nunavut</summary>
    [Description("Nunavut")]
    NU,

    /// <summary>Yukon</summary>
    [Description("Yukon")]
    YT,
}

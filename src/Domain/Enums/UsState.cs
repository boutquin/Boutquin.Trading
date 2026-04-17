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
/// US states for state-level capital gains and dividend tax rates.
/// Includes high-tax states, zero-tax states, and a federal-only default.
/// </summary>
public enum UsState
{
    /// <summary>Federal tax only — no state tax applied. Default.</summary>
    [Description("Federal Only")]
    FederalOnly,

    /// <summary>California (13.3% top rate)</summary>
    [Description("California")]
    CA,

    /// <summary>New York (10.9% top rate)</summary>
    [Description("New York")]
    NY,

    /// <summary>New Jersey (10.75% top rate)</summary>
    [Description("New Jersey")]
    NJ,

    /// <summary>Connecticut (6.99% top rate)</summary>
    [Description("Connecticut")]
    CT,

    /// <summary>Massachusetts (9% long-term, 12% short-term)</summary>
    [Description("Massachusetts")]
    MA,

    /// <summary>Minnesota (9.85% top rate)</summary>
    [Description("Minnesota")]
    MN,

    /// <summary>Oregon (9.9% top rate)</summary>
    [Description("Oregon")]
    OR,

    /// <summary>Hawaii (11% top rate)</summary>
    [Description("Hawaii")]
    HI,

    /// <summary>Texas (no state income tax)</summary>
    [Description("Texas")]
    TX,

    /// <summary>Florida (no state income tax)</summary>
    [Description("Florida")]
    FL,

    /// <summary>Washington (7% on long-term gains above $270K)</summary>
    [Description("Washington")]
    WA,

    /// <summary>Nevada (no state income tax)</summary>
    [Description("Nevada")]
    NV,

    /// <summary>Wyoming (no state income tax)</summary>
    [Description("Wyoming")]
    WY,

    /// <summary>South Dakota (no state income tax)</summary>
    [Description("South Dakota")]
    SD,

    /// <summary>New Hampshire (3% on dividends/interest, phasing out)</summary>
    [Description("New Hampshire")]
    NH,
}

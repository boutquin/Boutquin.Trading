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
/// Investment account types across US and Canadian jurisdictions.
/// Each type has different tax treatment for contributions, gains, and withdrawals.
/// </summary>
public enum AccountType
{
    // Canadian accounts

    /// <summary>Canadian taxable (non-registered) account.</summary>
    Taxable_CA,

    /// <summary>Registered Retirement Savings Plan — tax-deferred, contributions deductible.</summary>
    RRSP,

    /// <summary>Tax-Free Savings Account — no tax on growth, contributions not deductible.</summary>
    TFSA,

    /// <summary>Registered Education Savings Plan.</summary>
    RESP,

    /// <summary>First Home Savings Account (introduced 2023).</summary>
    FHSA,

    /// <summary>Locked-In Retirement Account.</summary>
    LIRA,

    /// <summary>Registered Retirement Income Fund.</summary>
    RRIF,

    // US accounts

    /// <summary>US taxable (non-registered) account.</summary>
    Taxable_US,

    /// <summary>Traditional IRA — tax-deferred, contributions may be deductible.</summary>
    TraditionalIRA,

    /// <summary>Roth IRA — no tax on qualified withdrawals, contributions not deductible.</summary>
    RothIRA,

    /// <summary>401(k) — employer-sponsored tax-deferred.</summary>
    FourOhOneK,

    /// <summary>Roth 401(k) — employer-sponsored, no tax on qualified withdrawals.</summary>
    RothFourOhOneK,

    // Generic

    /// <summary>Generic taxable account (jurisdiction not specified).</summary>
    Taxable
}

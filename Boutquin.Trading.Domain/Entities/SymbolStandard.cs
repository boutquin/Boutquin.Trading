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

using Boutquin.Trading.Domain.Enums;

namespace Boutquin.Trading.Domain.Entities;

using System;
using Boutquin.Domain.Extensions;
using Boutquin.Domain.Helpers;

/// <summary>
/// Represents a security symbol standard.
/// </summary>
public sealed class SymbolStandard
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolStandard"/> class.
    /// </summary>
    /// <param name="id">The Code of the symbol standard.</param>
    /// <param name="description">The description of the security symbol standard.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="description"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="description"/> length is not within the valid range, or 
    /// when <paramref name="id"/> is not defined in the <see cref="SecuritySymbolStandard"/> enumeration.
    /// </exception>
    public SymbolStandard(
        SecuritySymbolStandard id,
        string? description = null)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => id);
        if (!description.IsNullOrWhiteSpace())
        {
            Guard.AgainstOverflow(() => description, ColumnConstants.SymbolStandard_Description_Length);
        }

        Id = id;
        Description = description.IsNullOrWhiteSpace() ? id.GetDescription() : description;
    }

    /// <summary>
    /// Gets the Id of the security symbol standard.
    /// </summary>
    public SecuritySymbolStandard Id { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the description of the security symbol standard.
    /// </summary>
    public string Description { get; private set; } // Setter is for EF
}

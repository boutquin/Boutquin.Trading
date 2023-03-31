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
using Boutquin.Domain.Helpers;

/// <summary>
/// Represents an asset class.
/// </summary>
public sealed class AssetClass
{
    /// <summary>
    /// Gets the Code of the asset class.
    /// </summary>
    public AssetClassCode Code { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the name of the asset class.
    /// </summary>
    public string Name { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the description of the asset class.
    /// </summary>
    public string Description { get; private set; } // Setter is for EF

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetClass"/> class.
    /// </summary>
    /// <param name="code">The Code of the asset class.</param>
    /// <param name="name">The name of the asset class.</param>
    /// <param name="description">The description of the asset class.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="description"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length or <paramref name="description"/> length is not within the valid range, or when <paramref name="code"/> is not defined in the enumeration.
    /// </exception>
    public AssetClass(
        AssetClassCode code, 
        string name, 
        string description)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(code, nameof(code));
        Guard.AgainstNullOrWhiteSpaceAndOverflow(name, nameof(name), ColumnConstants.AssetClass_Name_Length);
        Guard.AgainstNullOrWhiteSpaceAndOverflow(description, nameof(description), ColumnConstants.AssetClass_Description_Length);

        Code = code;
        Name = name;
        Description = description;
    }
}

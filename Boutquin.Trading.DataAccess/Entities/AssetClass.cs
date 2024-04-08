// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
namespace Boutquin.Trading.DataAccess.Entities;

using Boutquin.Domain.Extensions;

/// <summary>
/// Represents an asset class.
/// </summary>
public sealed class AssetClass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetClass"/> class.
    /// </summary>
    /// <param name="id">The Code of the asset class.</param>
    /// <param name="description">The description of the asset class.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="id"/> is not defined in the <see cref="AssetClassCode"/> enumeration.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the <paramref name="description"/> is longer than the allowed length.
    /// </exception>
    public AssetClass(
        AssetClassCode id,
        string? description = null)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(() => id); // Throws ArgumentOutOfRangeException
        if (!description.IsNullOrWhiteSpace())
        {
            Guard.AgainstOverflow(() => description, 
                ColumnConstants.AssetClass_Description_Length); // Throws ArgumentOutOfRangeException
        }

        Id = id;
        Description = description.IsNullOrWhiteSpace() ? id.GetDescription() : description;
    }

    /// <summary>
    /// Gets the Code of the asset class.
    /// </summary>
    public AssetClassCode Id { get; private set; } // Setter is for EF

    /// <summary>
    /// Gets the description of the asset class.
    /// </summary>
    public string Description { get; private set; } // Setter is for EF
}

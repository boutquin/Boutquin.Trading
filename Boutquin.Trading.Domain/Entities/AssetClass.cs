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

/// <summary>
/// Represents an asset class.
/// </summary>
public sealed class AssetClass
{
    /// <summary>
    /// Gets the Id of the asset class.
    /// </summary>
    public AssetClassCode Id { get; }

    /// <summary>
    /// Gets the name of the asset class.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of the asset class.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetClass"/> class.
    /// </summary>
    /// <param name="id">The Id of the asset class.</param>
    /// <param name="name">The name of the asset class.</param>
    /// <param name="description">The description of the asset class.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="description"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length or <paramref name="description"/> length is not within the valid range, or when <paramref name="id"/> is not defined in the enumeration.
    /// </exception>
    public AssetClass(
        AssetClassCode id, 
        string name, 
        string description)
    {
        if (!Enum.IsDefined(typeof(AssetClassCode), id))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Invalid asset class code.");
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name), "Name cannot be null.");
        }

        if (name.Length == 0 || name.Length > ColumnConstants.AssetClass_Name_Length)
        {
            throw new ArgumentOutOfRangeException(nameof(name), $"Name must be between 1 and {ColumnConstants.AssetClass_Name_Length} characters.");
        }

        if (description == null)
        {
            throw new ArgumentNullException(nameof(description), "Description cannot be null.");
        }

        if (description.Length == 0 || description.Length > ColumnConstants.AssetClass_Description_Length)
        {
            throw new ArgumentOutOfRangeException(nameof(description), $"Description must be between 1 and {ColumnConstants.AssetClass_Description_Length} characters.");
        }

        Id = id;
        Name = name;
        Description = description;
    }
}

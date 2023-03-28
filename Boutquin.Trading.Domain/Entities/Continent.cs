﻿// Copyright (c) 2023 Pierre G. Boutquin. All rights reserved.
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
/// Represents a continent.
/// </summary>
public sealed class Continent
{
    /// <summary>
    /// Gets the code of the continent.
    /// </summary>
    public ContinentCode Code { get; }

    /// <summary>
    /// Gets the name of the continent.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Continent"/> class.
    /// </summary>
    /// <param name="code">The code of the continent.</param>
    /// <param name="name">The name of the continent.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="name"/> length is not within the valid range, or when <paramref name="code"/> is not defined in the <see cref="ContinentCode"/> enumeration.
    /// </exception>
    public Continent(
        ContinentCode code, 
        string name)
    {
        // Validate parameters
        Guard.AgainstUndefinedEnumValue(code, nameof(code));
        Guard.AgainstNullOrWhiteSpaceAndOverflow(name, nameof(name), ColumnConstants.Continent_Name_Length);

        Code = code;
        Name = name;
    }
}
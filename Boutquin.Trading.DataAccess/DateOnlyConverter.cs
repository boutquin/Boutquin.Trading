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
namespace Boutquin.Trading.DataAccess;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// A sealed class that provides a value converter between <see cref="DateOnly"/> and <see cref="DateTime"/> types.
/// </summary>
/// <remarks>
/// The <see cref="DateOnlyConverter"/> class is a custom value converter that allows Entity Framework Core
/// to store <see cref="DateOnly"/> type properties as <see cref="DateTime"/> in a database.
/// This can be helpful when working with databases that do not natively support the <see cref="DateOnly"/> type.
///
/// The converter automatically converts a <see cref="DateOnly"/> value to a <see cref="DateTime"/> value with
/// a minimal <see cref="TimeOnly"/> value (00:00:00) when storing data in the database. When reading data
/// from the database, the converter extracts the date part of the stored <see cref="DateTime"/> value and
/// converts it back to a <see cref="DateOnly"/> value.
///
/// To use this converter, register it in your <see cref="DbContext"/> class for the corresponding <see cref="DateOnly"/>
/// property using the <see cref="ModelBuilder"/> in the <c>OnModelCreating</c> method.
/// </remarks>
/// <example>
/// Here is an example of how to register the <see cref="DateOnlyConverter"/> for a <see cref="DateOnly"/> property:
/// <code>
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     modelBuilder.Entity<MyEntity>()
///         .Property(e => e.MyDateOnlyProperty)
///         .HasConversion(new DateOnlyConverter());
/// }
/// </code>
/// </example>
internal sealed class DateOnlyConverter : ValueConverter<DateOnly, DateTime>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateOnlyConverter"/> class.
    /// </summary>
    /// <remarks>
    /// This constructor sets up the conversion functions between <see cref="DateOnly"/> and <see cref="DateTime"/> types.
    /// </remarks>
    public DateOnlyConverter()
        : base(d => d.ToDateTime(TimeOnly.MinValue),
               d => DateOnly.FromDateTime(d))
    {
    }
}

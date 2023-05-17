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
namespace Boutquin.Trading.Domain.Interfaces;

using Exceptions;

/// <summary>
/// Represents an interface for reading symbols from a data source.
/// </summary>
public interface ISymbolReader
{
    /// <summary>
    /// Reads the symbols asynchronously from the data source.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation, containing an IEnumerable of symbols
    /// as strings when completed.
    /// </returns>
    /// <exception cref="SymbolReaderException">
    /// Thrown when an error occurs while reading symbols from the data source.
    /// </exception>
    Task<IEnumerable<string>> ReadSymbolsAsync();
}

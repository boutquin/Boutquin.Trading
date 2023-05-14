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

using System.Security;
using Boutquin.Trading.Domain.Exceptions;
using Boutquin.Trading.Domain.Interfaces;

namespace Boutquin.Trading.Domain.Helpers;

public sealed class CsvSymbolReader : ISymbolReader
{
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the CsvSymbolReader class.
    /// </summary>
    /// <param name="filePath">The path to the CSV file containing symbols.</param>
    public CsvSymbolReader(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Symbol file not found at '{_filePath}'.", _filePath);
        }
    }

    /// <summary>
    /// Reads symbols from a CSV file.
    /// </summary>
    /// <returns>A task representing the asynchronous operation that returns an IEnumerable of symbols.</returns>
    public async Task<IEnumerable<string>> ReadSymbolsAsync()
    {
        try
        {
            var symbols = new List<string>();

            await using var fileStream = File.OpenRead(_filePath);
            using var streamReader = new StreamReader(fileStream);

            while (await streamReader.ReadLineAsync() is { } line)
            {
                symbols.Add(line);
            }

            return symbols;
        }
        catch (IOException ex)
        {
            throw new SymbolReaderException($"Error reading symbol CSV file '{_filePath}'", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SymbolReaderException($"Access denied to symbol CSV file '{_filePath}'", ex);
        }
        catch (SecurityException ex)
        {
            throw new SymbolReaderException($"Security error accessing symbol CSV file '{_filePath}'", ex);
        }
    }
}

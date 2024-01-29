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

namespace Boutquin.Trading.Domain.Exceptions;

/// <summary>
/// The exception that is thrown when an error occurs during market data retrieval.
/// </summary>
public sealed class MarketDataRetrievalException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataRetrievalException"/> class.
    /// </summary>
    public MarketDataRetrievalException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataRetrievalException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <example>
    /// <code>
    /// if (marketData == null)
    /// {
    ///     throw new MarketDataRetrievalException("Failed to retrieve market data.");
    /// }
    /// </code>
    /// </example>
    public MarketDataRetrievalException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataRetrievalException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Retrieve market data here
    /// }
    /// catch (Exception ex)
    /// {
    ///     throw new MarketDataRetrievalException("Failed to retrieve market data.", ex);
    /// }
    /// </code>
    /// </example>
    public MarketDataRetrievalException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

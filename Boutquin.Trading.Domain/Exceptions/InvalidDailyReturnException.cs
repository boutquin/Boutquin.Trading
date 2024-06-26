﻿// Copyright (c) 2023-2024 Pierre G. Boutquin. All rights reserved.
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
/// Represents errors that occur when an invalid daily return value is encountered in the input data.
/// </summary>
/// <remarks>
/// An InvalidDailyReturnException is thrown when a daily return value is less than -1 (i.e., -100%),
/// which would result in a negative investment value. This exception provides a clear indication
/// of the specific issue with the input data.
/// </remarks>
[Serializable]
public sealed class InvalidDailyReturnException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidDailyReturnException"/> class with a
    /// specified error message.
    /// </summary>
    /// <param name="message">A message that describes the error.</param>
    public InvalidDailyReturnException(string message) : base(message) 
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidDailyReturnException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
    /// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero (0).</exception>
    private InvalidDailyReturnException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

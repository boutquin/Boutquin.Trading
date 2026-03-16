// Copyright (c) 2023-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Trading.Domain.Helpers;

using System.Collections;

/// <summary>
/// A fixed-capacity circular buffer that drops the oldest element when full.
/// Elements are maintained in chronological (insertion) order.
/// </summary>
/// <typeparam name="T">The type of elements stored in the window.</typeparam>
public sealed class RollingWindow<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private int _head; // Next write position
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="RollingWindow{T}"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of elements the window can hold. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is zero or negative.</exception>
    public RollingWindow(int capacity)
    {
        Guard.AgainstNegativeOrZero(() => capacity);

        Capacity = capacity;
        _buffer = new T[capacity];
    }

    /// <summary>
    /// Gets the maximum number of elements this window can hold.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the current number of elements in the window.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the window has reached its capacity.
    /// </summary>
    public bool IsFull => _count == Capacity;

    /// <summary>
    /// Gets the element at the specified index, where 0 is the oldest element.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside the valid range.</exception>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {_count - 1}.");
            }

            // The oldest element starts at (_head - _count + Capacity) % Capacity
            var actualIndex = (_head - _count + index + Capacity) % Capacity;
            return _buffer[actualIndex];
        }
    }

    /// <summary>
    /// Adds an element to the window. If the window is full, the oldest element is dropped.
    /// </summary>
    /// <param name="item">The element to add.</param>
    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % Capacity;

        if (_count < Capacity)
        {
            _count++;
        }
    }

    /// <summary>
    /// Returns all elements in chronological order as an array.
    /// </summary>
    /// <returns>An array of elements in the order they were added (oldest first).</returns>
    public T[] ToArray()
    {
        var result = new T[_count];
        for (var i = 0; i < _count; i++)
        {
            result[i] = this[i];
        }

        return result;
    }

    /// <summary>
    /// Removes all elements from the window.
    /// </summary>
    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_buffer);
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

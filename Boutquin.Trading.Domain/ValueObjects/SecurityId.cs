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

using Boutquin.Domain.Helpers;

namespace Boutquin.Trading.Domain.ValueObjects
{
    /// <summary>
    /// Represents a security identifier (ID) in the system.
    /// See alse: https://www.fearofoblivion.com/dont-let-ef-call-the-shots
    /// </summary>
    public readonly struct SecurityId : IEquatable<SecurityId>, IComparable<SecurityId>
    {
        /// <summary>
        /// The internal security ID value.
        /// </summary>
        private readonly int _id;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityId"/> struct.
        /// </summary>
        /// <param name="id">The security ID value.</param>
        public SecurityId(int id)
        {
            _id = id;
        }

        /// <summary>
        /// Returns a string representation of the security ID.
        /// </summary>
        /// <returns>A string containing the security ID value.</returns>
        public override string ToString()
        {
            return _id.ToString();
        }

        /// <summary>
        /// Returns a hash code for the current <see cref="SecurityId"/> instance.
        /// </summary>
        /// <returns>A hash code for the current <see cref="SecurityId"/> instance.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine("SecurityId", _id);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="SecurityId"/> instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is SecurityId other && Equals(other);

        /// <summary>
        /// Determines whether the specified <see cref="SecurityId"/> is equal to the current <see cref="SecurityId"/> instance.
        /// </summary>
        /// <param name="other">The other <see cref="SecurityId"/> to compare with the current instance.</param>
        /// <returns>true if the specified <see cref="SecurityId"/> is equal to the current instance; otherwise, false.</returns>
        public bool Equals(SecurityId other) => _id == other._id;

        /// <summary>
        /// Equality operator for two <see cref="SecurityId"/> instances.
        /// </summary>
        /// <param name="a">The first <see cref="SecurityId"/> instance.</param>
        /// <param name="b">The second <see cref="SecurityId"/> instance.</param>
        /// <returns>true if the two instances are equal; otherwise, false.</returns>
        public static bool operator ==(SecurityId a, SecurityId b) => a._id == b._id;

        /// <summary>
        /// Inequality operator for two <see cref="SecurityId"/> instances.
        /// </summary>
        /// <param name="a">The first <see cref="SecurityId"/> instance.</param>
        /// <param name="b">The second <see cref="SecurityId"/> instance.</param>
        /// <returns>true if the two instances are not equal; otherwise, false.</returns>
        public static bool operator !=(SecurityId a, SecurityId b) => a._id != b._id;

        /// <summary>
        /// Equality operator for a <see cref="SecurityId"/> instance and an integer value.
        /// </summary>
        /// <param name="a">The <see cref="SecurityId"/> instance.</param>
        /// <param name="b">The integer value.</param>
        /// <returns>true if the <see cref="SecurityId"/> instance is equal to the integer value; otherwise, false.</returns>
        public static bool operator ==(SecurityId a, int b) => a._id == b;

        /// <summary>
        /// Inequality operator for a <see cref="SecurityId"/> instance and an integer value.
        /// </summary>
        /// <param name="a">The <see cref="SecurityId"/> instance.</param>
        /// <param name="b">The integer value.</param>
        /// <returns>true if the <see cref="SecurityId"/> instance is not equal to the integer value; otherwise, false.</returns>
        public static bool operator !=(SecurityId a, int b) => a._id != b;

        /// <summary>
        /// Compares the current instance with another <see cref="SecurityId"/> and returns an integer that indicates
        /// their relative position in the sort order.
        /// </summary>
        /// <param name="other">The <see cref="SecurityId"/> to compare to this instance.</param>
        /// <returns>A signed integer that indicates the relative order of the objects being compared.</returns>
        public int CompareTo(SecurityId other)
        {
            return _id.CompareTo(other._id);
        }

        /// <summary>
        /// Converts a <see cref="SecurityId"/> object to an integer value.
        /// </summary>
        /// <param name="securityId">The <see cref="SecurityId"/> instance to convert.</param>
        /// <returns>An integer value representing the security ID.</returns>
        public static explicit operator int(SecurityId securityId)
        {
            return securityId._id;
        }

        /// <summary>
        /// Creates a new SecurityId instance.
        /// </summary>
        /// <param name="id">The security ID value.</param>
        /// <returns>A new SecurityId instance with the specified value.</returns>
        public static SecurityId Create(int id)
        {
            // Validate parameters
            Guard.AgainstNegativeOrZero(id, nameof(id));

            return new SecurityId(id);
        }
    }
}

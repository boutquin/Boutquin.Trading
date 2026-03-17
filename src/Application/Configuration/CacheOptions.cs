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

namespace Boutquin.Trading.Application.Configuration;

/// <summary>
/// Configuration options for the caching layer.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Directory for L2 CSV cache files. Null disables L2 caching.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Enable L1 in-process memory cache. Default: true.
    /// </summary>
    public bool EnableMemoryCache { get; set; } = true;
}

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
/// Configuration options for the trading calendar.
/// </summary>
/// <remarks>
/// Valid values for <see cref="TradingCalendar"/>: "US", "Canadian", "Composite".
/// When "Composite", <see cref="Calendars"/> must list constituent calendars
/// and <see cref="CompositionMode"/> must be "Any" or "All".
/// </remarks>
public sealed class CalendarOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Calendar";

    /// <summary>
    /// Gets or sets the trading calendar type. Valid: "US", "Canadian", "Composite".
    /// </summary>
    public string TradingCalendar { get; set; } = "US";

    /// <summary>
    /// Gets or sets the composition mode for composite calendars. Valid: "Any", "All".
    /// </summary>
    public string CompositionMode { get; set; } = "All";

    /// <summary>
    /// Gets or sets the constituent calendar names for composite mode. Valid items: "US", "Canadian".
    /// </summary>
    public List<string> Calendars { get; set; } = [];
}

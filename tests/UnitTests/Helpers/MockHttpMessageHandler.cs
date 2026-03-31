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

using System.Net;
using System.Text;

namespace Boutquin.Trading.Tests.UnitTests.Helpers;

/// <summary>
/// Mock HTTP message handler for testing. Supports both single fixed response
/// and per-URL response mapping for multi-symbol fetcher tests.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string? _defaultResponseBody;
    private readonly byte[]? _defaultResponseBytes;
    private readonly HttpStatusCode _defaultStatusCode;
    private readonly Dictionary<string, (string Body, HttpStatusCode Status)> _urlResponses;

    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> AllRequests { get; } = [];

    /// <summary>
    /// Creates a handler that returns the same response for all requests (backward compatible).
    /// </summary>
    public MockHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _defaultResponseBody = responseBody ?? throw new ArgumentNullException(nameof(responseBody));
        _defaultStatusCode = statusCode;
        _urlResponses = new Dictionary<string, (string, HttpStatusCode)>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a handler that returns the same binary response for all requests (for ZIP/binary content).
    /// </summary>
    public MockHttpMessageHandler(byte[] responseBytes, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _defaultResponseBytes = responseBytes ?? throw new ArgumentNullException(nameof(responseBytes));
        _defaultStatusCode = statusCode;
        _urlResponses = new Dictionary<string, (string, HttpStatusCode)>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a handler with per-URL response mapping. Uses substring matching if exact match fails.
    /// </summary>
    public MockHttpMessageHandler(Dictionary<string, (string Body, HttpStatusCode Status)> urlResponses)
    {
        _urlResponses = urlResponses ?? throw new ArgumentNullException(nameof(urlResponses));
        _defaultResponseBody = null;
        _defaultStatusCode = HttpStatusCode.NotFound;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        AllRequests.Add(request);

        var url = request.RequestUri?.ToString() ?? string.Empty;

        // Try exact match first
        if (_urlResponses.TryGetValue(url, out var match))
        {
            return CreateResponse(match.Body, match.Status);
        }

        // Then substring match
        foreach (var kvp in _urlResponses)
        {
            if (url.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return CreateResponse(kvp.Value.Body, kvp.Value.Status);
            }
        }

        // Fall back to default
        if (_defaultResponseBytes != null)
        {
            return CreateBinaryResponse(_defaultResponseBytes, _defaultStatusCode);
        }

        if (_defaultResponseBody != null)
        {
            return CreateResponse(_defaultResponseBody, _defaultStatusCode);
        }

        return CreateResponse("{}", HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> CreateResponse(string body, HttpStatusCode status) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    private static Task<HttpResponseMessage> CreateBinaryResponse(byte[] bytes, HttpStatusCode status) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(bytes)
        });
}

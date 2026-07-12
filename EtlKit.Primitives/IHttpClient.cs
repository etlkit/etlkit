using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace EtlKit.Primitives
{
    /// <summary>
    /// HTTP abstraction used by web-based sources and destinations, allowing the underlying HTTP
    /// client implementation to be swapped or mocked.
    /// </summary>
    public interface IHttpClient : IDisposable
    {
        /// <summary>
        /// Sends an HTTP request and returns the response body as a string.
        /// </summary>
        /// <param name="url">The request URL.</param>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="headers">Request headers to send, or <see langword="null"/> for none.</param>
        /// <param name="body">The request body, or <see langword="null"/> for none.</param>
        /// <returns>The response body.</returns>
        Task<string> InvokeAsync(
            string url,
            HttpMethod method,
            IDictionary<string, string> headers,
            string body
        );
    }
}

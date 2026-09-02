using System.Collections.Generic;

namespace EtlKit.Rest.Models
{
    /// <summary>
    /// Describes one REST call: HTTP method and headers used as-is, plus <see cref="Url"/> and <see
    /// cref="Body"/> as <a href="https://shopify.github.io/liquid/">Liquid</a> templates rendered
    /// against the input row.
    /// </summary>
    public sealed class RestMethodInfo
    {
        /// <summary>
        /// URL template (Liquid format)
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Array of headers
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// { GET, POST, PUT, DELETE }
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Request body template (Liquid format)
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// Number of request retries
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Pause between retry attempts (seconds)
        /// </summary>
        public double RetryInterval { get; set; }
    }
}

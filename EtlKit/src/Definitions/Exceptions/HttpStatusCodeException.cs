using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;

namespace EtlKit
{
    /// <summary>
    /// HTTP status code exception.
    /// </summary>
    [Serializable]
    public class HttpStatusCodeException : Exception
    {
        /// <summary>
        /// HTTP result code returned by the server.
        /// </summary>
        public HttpStatusCode HttpCode { get; }

        /// <summary>
        /// Full literal content of the response (as returned by <see cref="HttpContent.ReadAsStringAsync"/>).
        /// </summary>
        public string Content { get; }

        public HttpStatusCodeException(HttpStatusCode statusCode, string content)
            : base(content)
        {
            HttpCode = statusCode;
            Content = content;
        }

        protected HttpStatusCodeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            HttpCode = (HttpStatusCode)info.GetValue(nameof(HttpCode), typeof(HttpStatusCode));
            Content = info.GetString(nameof(Content))!;
        }
    }
}

using System.IO;
using System.Net.Http;

namespace EtlKit.Helper
{
    internal static class HttpRequestMessageExtensions
    {
        internal static HttpRequestMessage Clone(this HttpRequestMessage request)
        {
            var httpRequestMessage = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content.Clone(),
                Version = request.Version
            };
            foreach (var property in request.Properties)
                httpRequestMessage.Properties.Add(property);
            foreach (var header in request.Headers)
                httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
            return httpRequestMessage;
        }

        private static StreamContent Clone(this HttpContent content)
        {
            if (content == null)
                return null;
            var stream = new MemoryStream();
            content.CopyToAsync(stream).Wait();
            stream.Position = 0L;
            var streamContent = new StreamContent(stream);
            foreach (var header in content.Headers)
                streamContent.Headers.Add(header.Key, header.Value);
            return streamContent;
        }
    }
}

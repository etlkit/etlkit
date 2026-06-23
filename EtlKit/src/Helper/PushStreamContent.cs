using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace EtlKit.Helper
{
    internal sealed class PushStreamContent : HttpContent
    {
        private readonly Func<Stream, HttpContent, TransportContext, Task> _onStreamAvailable;

        public PushStreamContent(
            Func<Stream, HttpContent, TransportContext, Task> onStreamAvailable,
            string mediaType
        )
            : this(onStreamAvailable, new MediaTypeHeaderValue(mediaType)) { }

        private PushStreamContent(
            Func<Stream, HttpContent, TransportContext, Task> onStreamAvailable,
            MediaTypeHeaderValue mediaType
        )
        {
            _onStreamAvailable =
                onStreamAvailable ?? throw new ArgumentException(nameof(onStreamAvailable));
            Headers.ContentType = mediaType ?? ApplicationOctetStreamMediaType;
        }

        private static MediaTypeHeaderValue ApplicationOctetStreamMediaType =>
            new("application/octet-stream");

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext context
        )
        {
            var pushStreamContent = this;
            var serializeToStreamTask = new TaskCompletionSource<bool>();
            using var stream1 = new CompleteTaskOnCloseStream(stream, serializeToStreamTask);
            await pushStreamContent
                ._onStreamAvailable(stream1, pushStreamContent, context)
                .ConfigureAwait(false);
            await serializeToStreamTask.Task.ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1L;
            return false;
        }

        [StructLayout(LayoutKind.Sequential, Size = 1)]
        private struct AsyncVoid { }

        private sealed class CompleteTaskOnCloseStream : DelegatingStream
        {
            private readonly TaskCompletionSource<bool> _serializeToStreamTask;

            public CompleteTaskOnCloseStream(
                Stream innerStream,
                TaskCompletionSource<bool> serializeToStreamTask
            )
                : base(innerStream)
            {
                _serializeToStreamTask =
                    serializeToStreamTask
                    ?? throw new ArgumentNullException(nameof(serializeToStreamTask));
            }

            protected override void Dispose(bool disposing)
            {
                _serializeToStreamTask.TrySetResult(true);
            }

            public override void Close()
            {
                _serializeToStreamTask.TrySetResult(true);
            }
        }
    }
}

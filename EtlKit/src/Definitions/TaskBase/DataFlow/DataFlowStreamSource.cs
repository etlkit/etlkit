using System.IO;
using System.Net.Http;
using System.Threading;
using EtlKit.Common.DataFlow;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Base class for sources that read from a file or HTTP stream, with optional pagination across
    /// multiple requests/files. Derived classes implement <see cref="InitReader"/>, <see
    /// cref="ReadAll"/>, and <see cref="CloseReader"/> to parse the opened <see cref="StreamReader"/>.
    /// </summary>
    /// <typeparam name="TOutput">Type of the rows produced by this source.</typeparam>
    [PublicAPI]
    public abstract class DataFlowStreamSource<TOutput> : DataFlowSource<TOutput>
    {
        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        protected DataFlowStreamSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowStreamSource(ILogger logger)
            : base(logger) { }

        /* Public properties */
        /// <summary>
        /// The Url of the webservice (e.g. https://test.com/foo) or the file name (relative or absolute)
        /// </summary>
        public string Uri
        {
            get { return _uri; }
            set
            {
                _uri = value;
                GetNextUri = _ => _uri;
                HasNextUri = _ => false;
            }
        }

        /// <summary>
        /// Computes the URI to request next, given the number of rows produced so far (<see
        /// cref="EtlKit.Common.DataFlow.DataFlowTask.ProgressCount"/>). Reset by the <see cref="Uri"/>
        /// setter to always return the same URI; override this instead of <see cref="Uri"/> to
        /// paginate across multiple requests or files.
        /// </summary>
        public Func<int, string> GetNextUri { get; set; }

        /// <summary>
        /// Determines whether another request/file should be read after the current one, given the
        /// number of rows produced so far. Reset by the <see cref="Uri"/> setter to always return
        /// <see langword="false"/> (single request).
        /// </summary>
        public Func<int, bool> HasNextUri { get; set; }

        /// <summary>
        /// Specifies the resource type. By default requests are made with HttpClient.
        /// Specify ResourceType.File if you want to read from a json file.
        /// </summary>
        public ResourceType ResourceType { get; set; }

        /// <summary>
        /// The HTTP client used when <see cref="ResourceType"/> is <see cref="EtlKit.ResourceType.Http"/>.
        /// </summary>
        public HttpClient HttpClient { get; set; } = new();

        /* Internal properties */
        /// <summary>
        /// The URI currently being (or last) read, as resolved by <see cref="GetNextUri"/>.
        /// </summary>
        protected string CurrentRequestUri { get; set; }

        /// <summary>
        /// The reader over the currently open file or HTTP response stream. Set up by <see
        /// cref="InitReader"/>; consumed by <see cref="ReadAll"/>.
        /// </summary>
        protected StreamReader StreamReader { get; set; }
        private bool WasStreamOpened { get; set; }

        private string _uri;

        /// <summary>
        /// Reads from <see cref="GetNextUri"/> repeatedly, for as long as <see cref="HasNextUri"/>
        /// returns <see langword="true"/>, calling <see cref="InitReader"/> then <see cref="ReadAll"/>
        /// for each URI, and completing <see
        /// cref="EtlKit.Common.DataFlow.DataFlowSource{TOutput}.Buffer"/> once done. Always runs <see
        /// cref="CloseReader"/> and closes the stream/client afterwards, even on failure.
        /// </summary>
        public override void Execute(CancellationToken cancellationToken)
        {
            LogStart();
            try
            {
                do
                {
                    CurrentRequestUri = GetNextUri(ProgressCount);
                    OpenStream(CurrentRequestUri);
                    InitReader();
                    WasStreamOpened = true;
                    ReadAll();
                } while (HasNextUri(ProgressCount));
                Buffer.Complete();
            }
            finally
            {
                if (WasStreamOpened)
                {
                    CloseReader();
                    CloseStream();
                }
            }
            LogFinish();
        }

        private void OpenStream(string uri) =>
            StreamReader =
                ResourceType == ResourceType.File
                    ? new StreamReader(uri)
                    : new StreamReader(HttpClient.GetStreamAsync(new Uri(uri)).Result);

        private void CloseStream()
        {
            HttpClient?.Dispose();
            StreamReader?.Dispose();
        }

        /// <summary>
        /// Called once per URI, after <see cref="StreamReader"/> is opened, to prepare for reading
        /// (e.g. skip a header row).
        /// </summary>
        protected abstract void InitReader();

        /// <summary>
        /// Reads all rows from <see cref="StreamReader"/> and posts them to <see
        /// cref="EtlKit.Common.DataFlow.DataFlowSource{TOutput}.Buffer"/>.
        /// </summary>
        protected abstract void ReadAll();

        /// <summary>
        /// Called once per URI, before the stream is closed, to release any reader-specific resources.
        /// </summary>
        protected abstract void CloseReader();
    }
}

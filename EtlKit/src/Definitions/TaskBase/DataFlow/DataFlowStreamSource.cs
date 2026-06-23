using System.IO;
using System.Net.Http;
using System.Threading;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
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

        public Func<int, string> GetNextUri { get; set; }
        public Func<int, bool> HasNextUri { get; set; }

        /// <summary>
        /// Specifies the resource type. By default requests are made with HttpClient.
        /// Specify ResourceType.File if you want to read from a json file.
        /// </summary>
        public ResourceType ResourceType { get; set; }

        public HttpClient HttpClient { get; set; } = new();

        /* Internal properties */
        protected string CurrentRequestUri { get; set; }
        protected StreamReader StreamReader { get; set; }
        private bool WasStreamOpened { get; set; }

        private string _uri;

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

        protected abstract void InitReader();
        protected abstract void ReadAll();
        protected abstract void CloseReader();
    }
}

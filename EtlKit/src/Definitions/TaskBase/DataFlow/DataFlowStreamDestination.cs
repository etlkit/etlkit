using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using EtlKit.Common.DataFlow;
using EtlKit.Helper;
using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Base class for destinations that write to a file or HTTP stream. Derived classes implement
    /// <see cref="InitStream"/>, <see cref="WriteIntoStream"/>, and <see cref="CloseStream"/> to
    /// serialize rows onto the opened <see cref="StreamWriter"/>.
    /// </summary>
    /// <typeparam name="TInput">Type of the rows accepted by this destination.</typeparam>
    [PublicAPI]
    public abstract class DataFlowStreamDestination<TInput> : DataFlowDestination<TInput>
    {
        /// <summary>
        /// Creates a new instance with no logger.
        /// </summary>
        protected DataFlowStreamDestination() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        protected DataFlowStreamDestination(ILogger logger)
            : base(logger) { }

        /* Public properties */
        /// <summary>
        ///   The Url of the webservice (e.g. https://test.com/foo) or the file name (relative or absolute)
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        ///   Specifies the resource type. ResourceType.
        ///   Specify ResourceType.File if you want to write into a file.
        /// </summary>
        public ResourceType ResourceType { get; set; }

        /// <summary>
        /// The writer rows are serialized onto. Created lazily by <see
        /// cref="CreateStreamWriterByResourceType"/> on the first row.
        /// </summary>
        protected StreamWriter StreamWriter { get; set; }

        /// <summary>
        /// The HTTP client used when <see cref="ResourceType"/> is <see cref="EtlKit.DataFlow.ResourceType.Http"/>.
        /// </summary>
        public HttpClient HttpClient { get; set; } = new();

        /// <summary>
        /// Cancels the underlying HTTP push-stream request when the destination is torn down.
        /// </summary>
        internal CancellationTokenSource BufferCancellationSource { get; set; } = new();

        /// <summary>
        /// The in-flight HTTP request sending rows to <see cref="Uri"/>, when <see
        /// cref="ResourceType"/> is <see cref="EtlKit.DataFlow.ResourceType.Http"/>.
        /// </summary>
        public Task<HttpResponseMessage> HttpResponseMessage { get; set; }

        /// <summary>
        /// The <c>Content-Type</c> header sent with the HTTP request. Defaults to <c>"text/plain"</c>.
        /// </summary>
        public string HttpContentType { get; set; } = "text/plain";

        /// <summary>
        /// Text encoding for <see cref="StreamWriter"/>. When <see langword="null"/>, the writer's
        /// default encoding is used.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// The HTTP request used to send rows, defaulting to an empty <see cref="HttpMethod.Post"/>
        /// request. Its <c>Content</c> is replaced with the row stream when writing starts.
        /// </summary>
        public HttpRequestMessage HttpRequestMessage { get; set; } = new(HttpMethod.Post, "");

        private TaskCompletionSource<bool> DoneWritingCompletionSource { get; set; }

        private TaskCompletionSource<bool> CanWriteCompletionSource { get; set; }

        /// <summary>
        /// Creates the target block that processes incoming rows via <see cref="WriteData"/>, and
        /// initializes <see cref="EtlKit.Common.DataFlow.DataFlowDestination{TInput}.Completion"/>.
        /// Derived classes call this once ready to accept rows.
        /// </summary>
        protected void InitTargetAction()
        {
            TargetAction = new ActionBlock<TInput>(WriteData);
            SetCompletionTask();
        }

        /// <summary>
        /// Writes one row: lazily opens <see cref="StreamWriter"/> via <see
        /// cref="CreateStreamWriterByResourceType"/> on the first call, then delegates to <see
        /// cref="WriteIntoStream"/>. Rows that are <see langword="null"/> are skipped.
        /// </summary>
        /// <param name="data">The row to write.</param>
        protected void WriteData(TInput data)
        {
            if (data is null)
                return;

            if (StreamWriter == null)
            {
                CreateStreamWriterByResourceType(Uri);
                CanWriteCompletionSource?.Task.Wait();
                InitStream();
            }

            WriteIntoStream(data);
        }

        private void CreateStreamWriterByResourceType(string uri)
        {
            if (ResourceType == ResourceType.File)
            {
                StreamWriter = new StreamWriter(uri);
            }
            else
            {
                CanWriteCompletionSource = new TaskCompletionSource<bool>();
                DoneWritingCompletionSource = new TaskCompletionSource<bool>();
                using var request = HttpRequestMessage.Clone();
                request.RequestUri = new Uri(Uri);
                var pushStreamContent = new PushStreamContent(
                    async (stream, _, _) =>
                    {
                        try
                        {
                            StreamWriter =
                                Encoding != null
                                    ? new StreamWriter(stream, Encoding)
                                    : new StreamWriter(stream);
                            CanWriteCompletionSource.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            CanWriteCompletionSource?.SetException(ex);
                        }

                        await DoneWritingCompletionSource.Task.ConfigureAwait(false);
                        stream?.Close();
                    },
                    HttpContentType
                );
                request.Content = pushStreamContent;
                HttpResponseMessage = HttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    BufferCancellationSource.Token
                );
            }
        }

        /// <summary>
        /// Runs <see cref="CloseStream"/> and closes <see cref="StreamWriter"/>; for HTTP resources,
        /// also signals the push stream to finish, waits for the response, and throws if the response
        /// status was not successful. Then runs the base cleanup (invoking <see
        /// cref="EtlKit.Common.DataFlow.DataFlowDestination{TInput}.OnCompletion"/> and logging).
        /// </summary>
        /// <exception cref="HttpRequestException">The HTTP response indicated a non-success status code.</exception>
        protected override void CleanUp()
        {
            CloseStream();

            StreamWriter?.Close();

            if (ResourceType == ResourceType.Http)
            {
                DoneWritingCompletionSource?.SetResult(true);

                HttpResponseMessage?.Result?.EnsureSuccessStatusCode();
                HttpResponseMessage?.Dispose();
            }

            OnCompletion?.Invoke();

            LogFinish();
        }

        /// <summary>
        /// Called once, after <see cref="StreamWriter"/> is opened, to write any header needed before
        /// the first row.
        /// </summary>
        protected abstract void InitStream();

        /// <summary>
        /// Serializes one row onto <see cref="StreamWriter"/>.
        /// </summary>
        /// <param name="data">The row to write.</param>
        protected abstract void WriteIntoStream(TInput data);

        /// <summary>
        /// Called once, before the stream is closed, to write any trailer needed after the last row.
        /// </summary>
        protected abstract void CloseStream();
    }
}

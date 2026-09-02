using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Implements <see cref="ILinkErrorSource.LinkErrorTo"/> for data flow components: buffers <see
    /// cref="EtlKitError"/> records and forwards them to a linked error target, separate from the
    /// component's normal output.
    /// </summary>
    [PublicAPI]
    public class ErrorHandler
    {
        /// <summary>
        /// Exposes <see cref="ErrorBuffer"/> as a source block for linking.
        /// </summary>
        public ISourceBlock<EtlKitError> ErrorSourceBlock => ErrorBuffer;

        /// <summary>
        /// Buffers error records until <see cref="LinkErrorTo"/> links them to a target. <see
        /// langword="null"/> until <see cref="LinkErrorTo"/> is called (see <see cref="HasErrorBuffer"/>).
        /// </summary>
        public BufferBlock<EtlKitError> ErrorBuffer { get; set; }

        /// <summary>
        /// Whether <see cref="LinkErrorTo"/> has been called and <see cref="ErrorBuffer"/> is ready to
        /// accept error records via <see cref="Send"/>.
        /// </summary>
        public bool HasErrorBuffer => ErrorBuffer != null;

        /// <summary>
        /// Creates <see cref="ErrorBuffer"/> and links it to <paramref name="target"/>, so error
        /// records sent via <see cref="Send"/> are routed there. The buffer completes once <paramref
        /// name="completion"/> finishes.
        /// </summary>
        /// <param name="target">The component that will receive error records.</param>
        /// <param name="completion">The owning component's completion task; signals when no more errors will be sent.</param>
        public void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target, Task completion)
        {
            ErrorBuffer = new BufferBlock<EtlKitError>();
            ErrorSourceBlock.LinkTo(target.TargetBlock, new DataflowLinkOptions());
            target.AddPredecessorCompletion(ErrorSourceBlock.Completion);
            completion.ContinueWith(_ => ErrorBuffer.Complete());
        }

        /// <summary>
        /// Posts an <see cref="EtlKitError"/> built from <paramref name="e"/> and <paramref
        /// name="jsonRow"/> to <see cref="ErrorBuffer"/>. Requires <see cref="HasErrorBuffer"/>.
        /// </summary>
        /// <param name="e">The exception that occurred while processing the row.</param>
        /// <param name="jsonRow">The offending row, serialized to JSON (see <see cref="ConvertErrorData{T}"/>).</param>
        public void Send(Exception e, string jsonRow)
        {
            ErrorBuffer
                .SendAsync(
                    new EtlKitError
                    {
                        Exception = e,
                        ErrorText = e.Message,
                        ReportTime = DateTime.Now,
                        RecordAsJson = jsonRow,
                    }
                )
                .Wait();
        }

        /// <summary>
        /// Serializes <paramref name="row"/> to JSON for inclusion in an <see cref="EtlKitError"/>. If
        /// serialization itself fails, returns the serialization exception's message instead of throwing.
        /// </summary>
        /// <typeparam name="T">Type of the row being serialized.</typeparam>
        /// <param name="row">The row to serialize.</param>
        public static string ConvertErrorData<T>(T row)
        {
            try
            {
                return JsonConvert.SerializeObject(row, new JsonSerializerSettings());
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EtlKit.Primitives;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace EtlKit.Common.DataFlow
{
    [PublicAPI]
    public class ErrorHandler
    {
        public ISourceBlock<EtlKitError> ErrorSourceBlock => ErrorBuffer;
        public BufferBlock<EtlKitError> ErrorBuffer { get; set; }
        public bool HasErrorBuffer => ErrorBuffer != null;

        public void LinkErrorTo(IDataFlowLinkTarget<EtlKitError> target, Task completion)
        {
            ErrorBuffer = new BufferBlock<EtlKitError>();
            ErrorSourceBlock.LinkTo(target.TargetBlock, new DataflowLinkOptions());
            target.AddPredecessorCompletion(ErrorSourceBlock.Completion);
            completion.ContinueWith(_ => ErrorBuffer.Complete());
        }

        public void Send(Exception e, string jsonRow)
        {
            ErrorBuffer
                .SendAsync(
                    new EtlKitError
                    {
                        Exception = e,
                        ErrorText = e.Message,
                        ReportTime = DateTime.Now,
                        RecordAsJson = jsonRow
                    }
                )
                .Wait();
        }

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

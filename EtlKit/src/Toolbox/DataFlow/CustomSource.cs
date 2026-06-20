using System.Threading;

using EtlKit.Primitives;

using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Define your own source block.
    /// </summary>
    /// <typeparam name="TOutput">Type of data output.</typeparam>
    [PublicAPI]
    public class CustomSource<TOutput> : DataFlowSource<TOutput>, IDataFlowSource<TOutput>
    {
        /* ITask Interface */
        public sealed override string TaskName => "Read data from custom source";

        /* Public properties */
        public Func<TOutput> ReadFunc { get; set; }
        public Func<bool> ReadCompletedFunc { get; set; }

        /* Private stuff */

        public CustomSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CustomSource(ILogger<CustomSource<TOutput>> logger)
            : base(logger) { }

        public CustomSource(Func<TOutput> readFunc, Func<bool> readCompletedFunc)
            : this()
        {
            ReadFunc = readFunc;
            ReadCompletedFunc = readCompletedFunc;
        }

        public CustomSource(string name, Func<TOutput> readFunc, Func<bool> readCompletedFunc)
            : this(readFunc, readCompletedFunc)
        {
            TaskName = name;
        }

        public override void Execute(CancellationToken cancellationToken)
        {
            LogStart();
            while (!ReadCompletedFunc.Invoke())
            {
                try
                {
                    Buffer.SendAsync(ReadFunc.Invoke(), cancellationToken).Wait(cancellationToken);
                }
                catch (Exception e)
                {
                    if (!ErrorHandler.HasErrorBuffer)
                        throw;
                    ErrorHandler.Send(e, e.Message);
                }
                LogProgress();
            }
            Buffer.Complete();
            LogFinish();
        }
    }

    /// <summary>
    /// Define your own source block. The non generic implementation returns a dynamic object as output.
    /// </summary>
    [PublicAPI]
    public class CustomSource : CustomSource<ExpandoObject>
    {
        public CustomSource() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public CustomSource(ILogger<CustomSource> logger)
            : base(logger) { }

        public CustomSource(Func<ExpandoObject> readFunc, Func<bool> readCompletedFunc)
            : base(readFunc, readCompletedFunc) { }

        public CustomSource(string name, Func<ExpandoObject> readFunc, Func<bool> readCompletedFunc)
            : base(name, readFunc, readCompletedFunc) { }
    }
}

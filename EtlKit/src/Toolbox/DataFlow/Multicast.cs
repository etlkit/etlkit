using EtlKit.Common.DataFlow;

using Microsoft.Extensions.Logging;

using DataFlow_TypeInfo = EtlKit.Common.DataFlow.TypeInfo;
using TypeInfo = EtlKit.Common.DataFlow.TypeInfo;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// A multicast duplicates data from the input into two outputs.
    /// </summary>
    /// <typeparam name="TInput">Type of input data.</typeparam>
    /// <example>
    /// <code>
    /// Multicast&lt;MyDataRow&gt; multicast = new Multicast&lt;MyDataRow&gt;();
    /// multicast.LinkTo(dest1);
    /// multicast.LinkTo(dest2);
    /// </code>
    /// </example>
    [PublicAPI]
    public class Multicast<TInput> : DataFlowTransformation<TInput, TInput>
    {
        /* ITask Interface */
        public sealed override string TaskName { get; set; } = "Multicast - duplicate data";

        /* Public Properties */
        public override ISourceBlock<TInput> SourceBlock => BroadcastBlock;
        public override ITargetBlock<TInput> TargetBlock => BroadcastBlock;

        /* Private stuff */
        internal BroadcastBlock<TInput> BroadcastBlock { get; set; }
        private DataFlow_TypeInfo TypeInfo { get; set; }
        private ObjectCopy<TInput> ObjectCopy { get; set; }

        public Multicast()
            : this(logger: null) { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public Multicast([CanBeNull] ILogger<Multicast<TInput>> logger)
            : base(logger)
        {
            TypeInfo = new DataFlow_TypeInfo(typeof(TInput)).GatherTypeInfo();
            ObjectCopy = new ObjectCopy<TInput>(TypeInfo);
            BroadcastBlock = new BroadcastBlock<TInput>(Clone);
        }

        public Multicast(string name)
            : this()
        {
            TaskName = name;
        }

        private TInput Clone(TInput row)
        {
            TInput clone = ObjectCopy.Clone(row);
            LogProgress();
            return clone;
        }
    }

    /// <summary>
    /// A multicast duplicates data from the input into two outputs. The non generic version or the multicast
    /// excepct a dynamic object as input and has two output with the copies of the input.
    /// </summary>
    /// <see cref="Multicast{TInput}"></see>
    /// <example>
    /// <code>
    /// //Non generic Multicast works with dynamic object as input and output
    /// Multicast multicast = new Multicast();
    /// multicast.LinkTo(dest1);
    /// multicast.LinkTo(dest2);
    /// </code>
    /// </example>
    [PublicAPI]
    public class Multicast : Multicast<ExpandoObject>
    {
        public Multicast() { }

        /// <summary>
        /// Creates a new instance with an injected logger.
        /// </summary>
        public Multicast(ILogger<Multicast> logger)
            : base(logger) { }

        public Multicast(string name)
            : base(name) { }
    }
}

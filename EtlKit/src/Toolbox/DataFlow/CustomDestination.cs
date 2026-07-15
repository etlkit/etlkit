using EtlKit.Common.DataFlow;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// Define your own destination block. The non generic implementation uses a dynamic object as input.
    /// </summary>
    [PublicAPI]
    public class CustomDestination : CustomDestination<ExpandoObject>
    {
        /// <summary>
        /// Creates a new instance with no write action set yet.
        /// </summary>
        public CustomDestination() { }

        /// <summary>
        /// Creates a new instance with the given write action.
        /// </summary>
        /// <param name="writeAction">Action invoked for each row written to this destination.</param>
        public CustomDestination(Action<ExpandoObject> writeAction)
            : base(writeAction) { }
    }
}

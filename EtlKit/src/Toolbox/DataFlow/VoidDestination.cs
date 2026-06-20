using EtlKit.Common.DataFlow;

namespace EtlKit.DataFlow
{
    /// <summary>
    /// This destination if used as a trash.
    /// Redirect all data in this destination which you do not want for further processing.
    /// Every records needs to be transferred to a destination to have a dataflow completed.
    /// The non generic implementation works with a dynamic obect as input.
    /// </summary>
    [PublicAPI]
    public class VoidDestination : VoidDestination<ExpandoObject> { }
}

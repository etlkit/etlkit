using System;
using System.Threading.Tasks.Dataflow;

namespace EtlKit.Primitives
{
    /// <summary>
    /// The source side of a data flow link: any component that can send rows to an
    /// <see cref="IDataFlowLinkTarget{TInput}"/>. See <c>docs/dataflow/linking-execution.md</c> for
    /// the full walkthrough with predicates and multicast.
    /// </summary>
    /// <typeparam name="TOutput">Type of the rows produced by this source.</typeparam>
    public interface IDataFlowLinkSource<out TOutput>
    {
        /// <summary>
        /// The TPL Dataflow block that produces rows sent to linked targets.
        /// </summary>
        ISourceBlock<TOutput> SourceBlock { get; }

        /// <summary>
        /// Links every row produced by this component to <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The component that will receive all rows.</param>
        /// <returns><paramref name="target"/>, to allow chaining further <c>LinkTo</c> calls.</returns>
        IDataFlowLinkSource<TOutput> LinkTo(IDataFlowLinkTarget<TOutput> target);

        /// <summary>
        /// Links only the rows matching <paramref name="predicate"/> to <paramref name="target"/>.
        /// </summary>
        /// <remarks>
        /// Rows that do not match <paramref name="predicate"/> are <b>not</b> automatically discarded;
        /// if no other link accepts them they remain unconsumed and can prevent the flow from
        /// completing. Use the two-predicate overload, or link a <c>VoidDestination</c> explicitly,
        /// to guarantee every row is drained somewhere.
        /// </remarks>
        /// <param name="target">The component that will receive matching rows.</param>
        /// <param name="predicate">Returns <see langword="true"/> for rows that should be sent to <paramref name="target"/>.</param>
        /// <returns><paramref name="target"/>, to allow chaining further <c>LinkTo</c> calls.</returns>
        IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        );

        /// <summary>
        /// Links rows matching <paramref name="rowsToKeep"/> to <paramref name="target"/>, and rows
        /// matching <paramref name="rowsIntoVoid"/> to an internal <c>VoidDestination</c> so every row
        /// is guaranteed to be consumed.
        /// </summary>
        /// <param name="target">The component that will receive rows matching <paramref name="rowsToKeep"/>.</param>
        /// <param name="rowsToKeep">Returns <see langword="true"/> for rows that should be sent to <paramref name="target"/>.</param>
        /// <param name="rowsIntoVoid">Returns <see langword="true"/> for rows that should be discarded.</param>
        /// <returns><paramref name="target"/>, to allow chaining further <c>LinkTo</c> calls.</returns>
        IDataFlowLinkSource<TOutput> LinkTo(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        );

        /// <summary>
        /// Links every row produced by this component to <paramref name="target"/>, returning the
        /// chain typed as <typeparamref name="TConvert"/> instead of <typeparamref name="TOutput"/>.
        /// Use this when <paramref name="target"/> is a transformation whose output type differs from
        /// its input type, e.g. <c>source.LinkTo&lt;OutputType&gt;(row).LinkTo(dest)</c>.
        /// </summary>
        /// <typeparam name="TConvert">Output type of <paramref name="target"/>, used for the returned chain.</typeparam>
        /// <param name="target">The component that will receive all rows.</param>
        /// <returns>
        /// <paramref name="target"/> cast to <see cref="IDataFlowLinkSource{TConvert}"/>, or <see
        /// langword="null"/> if it does not implement that interface.
        /// </returns>
        IDataFlowLinkSource<TConvert> LinkTo<TConvert>(IDataFlowLinkTarget<TOutput> target);

        /// <summary>
        /// Links only the rows matching <paramref name="predicate"/> to <paramref name="target"/>,
        /// returning the chain typed as <typeparamref name="TConvert"/>. See the non-generic overload
        /// for the predicate-drain caveat, and the class summary for the type-conversion use case.
        /// </summary>
        /// <typeparam name="TConvert">Output type of <paramref name="target"/>, used for the returned chain.</typeparam>
        /// <param name="target">The component that will receive matching rows.</param>
        /// <param name="predicate">Returns <see langword="true"/> for rows that should be sent to <paramref name="target"/>.</param>
        /// <returns>
        /// <paramref name="target"/> cast to <see cref="IDataFlowLinkSource{TConvert}"/>, or <see
        /// langword="null"/> if it does not implement that interface.
        /// </returns>
        IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> predicate
        );

        /// <summary>
        /// Links rows matching <paramref name="rowsToKeep"/> to <paramref name="target"/> and rows
        /// matching <paramref name="rowsIntoVoid"/> to an internal <c>VoidDestination</c>, returning
        /// the chain typed as <typeparamref name="TConvert"/>.
        /// </summary>
        /// <typeparam name="TConvert">Output type of <paramref name="target"/>, used for the returned chain.</typeparam>
        /// <param name="target">The component that will receive rows matching <paramref name="rowsToKeep"/>.</param>
        /// <param name="rowsToKeep">Returns <see langword="true"/> for rows that should be sent to <paramref name="target"/>.</param>
        /// <param name="rowsIntoVoid">Returns <see langword="true"/> for rows that should be discarded.</param>
        /// <returns>
        /// <paramref name="target"/> cast to <see cref="IDataFlowLinkSource{TConvert}"/>, or <see
        /// langword="null"/> if it does not implement that interface.
        /// </returns>
        IDataFlowLinkSource<TConvert> LinkTo<TConvert>(
            IDataFlowLinkTarget<TOutput> target,
            Predicate<TOutput> rowsToKeep,
            Predicate<TOutput> rowsIntoVoid
        );
    }
}

#nullable enable

namespace EtlKit.Primitives
{
    /// <summary>
    /// Processing result - union type for successful and erroneous outcomes, where successful processing
    /// does not produce any meaningful data (like <see langword="void"/>)
    /// </summary>
    /// <typeparam name="TInput">Input record type</typeparam>
    public class EtlKitResult<TInput>
    {
        /// <summary>
        /// true if processing succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Original process input
        /// </summary>
        public TInput Input { get; set; } = default!;

        /// <summary>
        /// Error details if <see cref="Success"/> is <see langword="false"/>
        /// </summary>
        public EtlKitError? Error { get; set; }
    }

    /// <summary>
    /// Processing result - union type for successful and erroneous outcomes
    /// </summary>
    /// <typeparam name="TInput">Input record type</typeparam>
    /// <typeparam name="TOutput">Successful result type</typeparam>
    public class EtlKitResult<TInput, TOutput> : EtlKitResult<TInput>
    {
        /// <summary>
        /// Output of successful processing
        /// </summary>
        public TOutput? Output { get; set; }
    }
}

namespace EtlKit.DataFlow
{
    /// <summary>
    /// The type info for mapping types. This type info is used to map the input data to the output data.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    public interface IMappingTypeInfo<in TInput, in TOutput>
    {
        /// <summary>
        /// Indicates if the input or output type is an array
        /// </summary>
        public bool IsArrayOutput { get; }

        /// <summary>
        /// Set the output value or throw an exception if the value can't be set
        /// </summary>
        /// <param name="outputRow">Output row object</param>
        /// <param name="value">Value to set</param>
        /// <param name="attributeMapping">Attribute to set value of</param>
        /// <param name="convertToUnderlyingType">Convert value to corresponding not nullable type</param>
        void SetOutputValueOrThrow(
            TOutput outputRow,
            object value,
            AttributeMappingInfo attributeMapping,
            bool convertToUnderlyingType
        );

        /// <summary>
        /// Get the input value from row
        /// </summary>
        /// <param name="inputRow">Input row object</param>
        /// <param name="attributeMapping">Attribute to get value from</param>
        object GetInputValue(TInput inputRow, AttributeMappingInfo attributeMapping);
    }
}

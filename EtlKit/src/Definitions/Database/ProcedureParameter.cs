namespace EtlKit
{
    /// <summary>
    /// Describes one parameter of a <see cref="ProcedureDefinition"/>.
    /// </summary>
    [PublicAPI]
    public class ProcedureParameter
    {
        /// <summary>
        /// The parameter's name, without the driver-specific prefix (e.g. <c>@</c>).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The parameter's SQL data type (e.g. <c>"INT"</c>, <c>"VARCHAR(50)"</c>).
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// The parameter's default value expression, or <see langword="null"/> if it has none.
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Whether <see cref="DefaultValue"/> is set.
        /// </summary>
        public bool HasDefaultValue => !string.IsNullOrWhiteSpace(DefaultValue);

        /// <summary>
        /// Whether the parameter is read-only (input only, not an output parameter).
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Whether the parameter is an output parameter.
        /// </summary>
        public bool Out { get; set; }

        private ProcedureParameter() { }

        /// <summary>
        /// Creates a parameter with the given name and data type, and no default value.
        /// </summary>
        /// <param name="name">The parameter's name.</param>
        /// <param name="dataType">The parameter's SQL data type.</param>
        public ProcedureParameter(string name, string dataType)
            : this()
        {
            Name = name;
            DataType = dataType;
        }

        /// <summary>
        /// Creates a parameter with the given name, data type, and default value.
        /// </summary>
        /// <param name="name">The parameter's name.</param>
        /// <param name="dataType">The parameter's SQL data type.</param>
        /// <param name="defaultValue">The parameter's default value expression.</param>
        public ProcedureParameter(string name, string dataType, string defaultValue)
            : this(name, dataType)
        {
            DefaultValue = defaultValue;
        }
    }
}

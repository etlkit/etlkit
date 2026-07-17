namespace EtlKit
{
    /// <summary>
    /// Describes a stored procedure: its name, body, and parameters, for use with tasks such as
    /// <c>CreateProcedureTask</c>.
    /// </summary>
    [PublicAPI]
    public class ProcedureDefinition
    {
        /// <summary>
        /// The procedure's name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The procedure body (the SQL executed when the procedure runs).
        /// </summary>
        public string Definition { get; set; }

        /// <summary>
        /// The procedure's parameters.
        /// </summary>
        public List<ProcedureParameter> Parameter { get; set; }

        /// <summary>
        /// Creates a definition with no name, body, or parameters set yet.
        /// </summary>
        public ProcedureDefinition()
        {
            Parameter = new List<ProcedureParameter>();
        }

        /// <summary>
        /// Creates a definition with the given name and body, and no parameters.
        /// </summary>
        /// <param name="name">The procedure's name.</param>
        /// <param name="definition">The procedure body.</param>
        public ProcedureDefinition(string name, string definition)
            : this()
        {
            Name = name;
            Definition = definition;
        }

        /// <summary>
        /// Creates a definition with the given name, body, and parameters.
        /// </summary>
        /// <param name="name">The procedure's name.</param>
        /// <param name="definition">The procedure body.</param>
        /// <param name="parameter">The procedure's parameters.</param>
        public ProcedureDefinition(
            string name,
            string definition,
            List<ProcedureParameter> parameter
        )
            : this(name, definition)
        {
            Parameter = parameter;
        }
    }
}

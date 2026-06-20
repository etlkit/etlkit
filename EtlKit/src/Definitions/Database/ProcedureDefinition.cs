namespace EtlKit
{
    [PublicAPI]
    public class ProcedureDefinition
    {
        public string Name { get; set; }
        public string Definition { get; set; }

        public List<ProcedureParameter> Parameter { get; set; }

        public ProcedureDefinition()
        {
            Parameter = new List<ProcedureParameter>();
        }

        public ProcedureDefinition(string name, string definition)
            : this()
        {
            Name = name;
            Definition = definition;
        }

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

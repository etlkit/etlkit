namespace EtlKit
{
    [PublicAPI]
    public class ProcedureParameter
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string DefaultValue { get; set; }
        public bool HasDefaultValue => !string.IsNullOrWhiteSpace(DefaultValue);
        public bool ReadOnly { get; set; }
        public bool Out { get; set; }

        private ProcedureParameter() { }

        public ProcedureParameter(string name, string dataType)
            : this()
        {
            Name = name;
            DataType = dataType;
        }

        public ProcedureParameter(string name, string dataType, string defaultValue)
            : this(name, dataType)
        {
            DefaultValue = defaultValue;
        }
    }
}

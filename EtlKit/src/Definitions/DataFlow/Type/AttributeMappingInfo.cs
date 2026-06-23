namespace EtlKit.DataFlow
{
    public record AttributeMappingInfo
    {
        internal PropertyInfo PropInInput { get; set; }
        internal string PropNameInInput { get; set; }
        internal PropertyInfo PropInOutput { get; set; }
        internal string PropNameInOutput { get; set; }
    }
}

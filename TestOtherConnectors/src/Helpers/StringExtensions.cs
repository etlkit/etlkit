namespace EtlKit.TestOtherConnectors.Helpers
{
    public static class StringExtensions
    {
        public static string NormalizeLineEndings(this string me)
        {
            return me.Replace("\r\n", Environment.NewLine);
        }
    }
}

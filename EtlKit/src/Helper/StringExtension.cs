namespace EtlKit.Helper
{
    /// <summary>
    /// Extension methods for <see cref="string"/>.
    /// </summary>
    public static class StringExtension
    {
        /// <summary>
        ///		This replicates the functionality of case-insensitive functionality built into Replace in .Net Core.
        /// </summary>
        /// <param name="toSearch">The string to search within.</param>
        /// <param name="find">The substring to find, matched case-insensitively.</param>
        /// <param name="replace">The replacement text.</param>
        /// <returns>
        /// <paramref name="toSearch"/> with the first case-insensitive match of <paramref name="find"/>
        /// replaced by <paramref name="replace"/>, or <paramref name="toSearch"/> unchanged if no match is found.
        /// </returns>
        public static string ReplaceIgnoreCase(this string toSearch, string find, string replace)
        {
            var index = toSearch.IndexOf(find, StringComparison.InvariantCultureIgnoreCase);

            if (index < 0)
            {
                return toSearch;
            }

            var replacement = toSearch.Substring(0, index) + replace;

            if (toSearch.Length > index + find.Length)
            {
                replacement += toSearch.Substring(index + find.Length);
            }

            return replacement;
        }
    }
}

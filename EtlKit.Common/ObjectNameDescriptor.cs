using System;
using System.Text.RegularExpressions;

namespace EtlKit.Common
{
    /// <summary>
    /// Parses a possibly-schema-qualified, possibly-quoted SQL object name (e.g. <c>[dbo].[MyTable]</c>)
    /// into its schema and table parts, and exposes both in quoted and unquoted form.
    /// </summary>
    public sealed class ObjectNameDescriptor
    {
        private string _schema;
        private string _table;

        /// <summary>
        /// The raw object name as passed to the constructor, e.g. <c>"[dbo].[MyTable]"</c>.
        /// </summary>
        public string ObjectName { get; }

        /// <summary>
        /// Quotation begin character used to recognize and produce quoted identifiers.
        /// </summary>
        public string QB { get; }

        /// <summary>
        /// Quotation end character used to recognize and produce quoted identifiers.
        /// </summary>
        public string QE { get; }

        /// <summary>
        /// The table/object name, quoted with <see cref="QB"/>/<see cref="QE"/> if it is not already.
        /// </summary>
        public string QuotedObjectName => _table.StartsWith(QB) ? _table : QB + _table + QE;

        /// <summary>
        /// The table/object name with any <see cref="QB"/>/<see cref="QE"/> quoting removed.
        /// </summary>
        public string UnquotedObjectName =>
            _table.StartsWith(QB)
                ? _table.Replace(QB, string.Empty).Replace(QE, string.Empty)
                : _table;

        /// <summary>
        /// The schema name with any <see cref="QB"/>/<see cref="QE"/> quoting removed, or an empty
        /// string if <see cref="ObjectName"/> had no schema part.
        /// </summary>
        public string UnquotedSchemaName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_schema))
                {
                    return string.Empty;
                }

                return _schema.StartsWith(QB)
                    ? _schema.Replace(QB, string.Empty).Replace(QE, string.Empty)
                    : _schema;
            }
        }

        /// <summary>
        /// The schema name, quoted with <see cref="QB"/>/<see cref="QE"/> if it is not already, or an
        /// empty string if <see cref="ObjectName"/> had no schema part.
        /// </summary>
        public string QuotedSchemaName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_schema))
                {
                    return string.Empty;
                }

                return _schema.StartsWith(QB) ? _schema : QB + _schema + QE;
            }
        }

        /// <summary>
        /// <see cref="QuotedSchemaName"/> and <see cref="QuotedObjectName"/> joined with a dot, or just
        /// <see cref="QuotedObjectName"/> if there is no schema.
        /// </summary>
        public string QuotedFullName =>
            string.IsNullOrWhiteSpace(_schema)
                ? QuotedObjectName
                : QuotedSchemaName + '.' + QuotedObjectName;

        /// <summary>
        /// <see cref="UnquotedSchemaName"/> and <see cref="UnquotedObjectName"/> joined with a dot, or
        /// just <see cref="UnquotedObjectName"/> if there is no schema.
        /// </summary>
        public string UnquotedFullName =>
            string.IsNullOrWhiteSpace(_schema)
                ? UnquotedObjectName
                : UnquotedSchemaName + '.' + UnquotedObjectName;

#pragma warning disable SP3110 // Identifier Spelling
        /// <summary>Obsolete alias for <see cref="QuotedObjectName"/>.</summary>
        [Obsolete("Please, use QuotedObjectName instead")]
        public string QuotatedObjectName => QuotedObjectName;

        /// <summary>Obsolete alias for <see cref="UnquotedObjectName"/>.</summary>
        [Obsolete("Please, use UnquotedObjectName instead")]
        public string UnquotatedObjectName => UnquotedObjectName;

        /// <summary>Obsolete alias for <see cref="QuotedSchemaName"/>.</summary>
        [Obsolete("Please, use QuotedSchemaName instead")]
        public string QuotatedSchemaName => QuotedSchemaName;

        /// <summary>Obsolete alias for <see cref="UnquotedSchemaName"/>.</summary>
        [Obsolete("Please, use UnquotedSchemaName instead")]
        public string UnquotatedSchemaName => UnquotedSchemaName;

        /// <summary>Obsolete alias for <see cref="QuotedFullName"/>.</summary>
        [Obsolete("Please, use QuotedFullName instead")]
        public string QuotatedFullName => QuotedFullName;

        /// <summary>Obsolete alias for <see cref="UnquotedFullName"/>.</summary>
        [Obsolete("Please, use UnquotedFullName instead")]
        public string UnquotatedFullName => UnquotedFullName;
#pragma warning restore SP3110 // Identifier Spelling


        /// <summary>
        /// Parses <paramref name="objectName"/> into its schema and table parts using <paramref
        /// name="qb"/>/<paramref name="qe"/> to recognize quoted identifiers.
        /// </summary>
        /// <param name="objectName">The (possibly schema-qualified, possibly quoted) object name to parse.</param>
        /// <param name="qb">Quotation begin character, e.g. <c>"["</c> or <c>"`"</c>.</param>
        /// <param name="qe">Quotation end character, e.g. <c>"]"</c> or <c>"`"</c>.</param>
        /// <exception cref="EtlKitException">The object name could not be parsed into schema/table parts.</exception>
        public ObjectNameDescriptor(string objectName, string qb, string qe)
        {
            ObjectName = objectName;
            QB = qb;
            QE = qe;

            ParseSchemaAndTable();
        }

        private void ParseSchemaAndTable()
        {
            MatchCollection m = Regex.Matches(ObjectName, Expr, RegexOptions.IgnoreCase);
            switch (m.Count)
            {
                case 0:
                    throw new EtlKitException(
                        $"Unable to retrieve object name (and possible schema) from {ObjectName}."
                    );
                case > 2:
                    throw new EtlKitException(
                        $"Unable to retrieve table and schema name from {ObjectName} - found {m.Count} possible matches."
                    );
                case 1:
                    _table = m[0].Value.Trim();
                    break;
                case 2:
                    _schema = m[0].Value.Trim();
                    _table = m[1].Value.Trim().StartsWith(".")
                        ? m[1].Value.Trim().Substring(1)
                        : m[1].Value.Trim();
                    break;
            }
        }

        private string Expr
        {
            get
            {
                var beginningQuote = QB switch
                {
                    "[" => @"\[",
                    "" => @"""",
                    _ => QB,
                };
                var endingQuote = QE switch
                {
                    "]" => @"\]",
                    "" => @"""",
                    _ => QB,
                };

                //see also: https://stackoverflow.com/questions/60747665/regex-expression-for-parsing-sql-server-schema-and-tablename?noredirect=1#comment107559387_60747665
                return $@"\.? *(?:{beginningQuote}[^{endingQuote}]+{endingQuote}|\w+)"; //Original Regex:  \.? *(?:\[[^]]+\]|\w+)
            }
        }
    }
}

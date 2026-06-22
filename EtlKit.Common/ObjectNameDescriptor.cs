using System;
using System.Text.RegularExpressions;

namespace EtlKit.Common
{
    public sealed class ObjectNameDescriptor
    {
        private string _schema;
        private string _table;

        public string ObjectName { get; }
        public string QB { get; }
        public string QE { get; }

        public string QuotedObjectName => _table.StartsWith(QB) ? _table : QB + _table + QE;
        public string UnquotedObjectName =>
            _table.StartsWith(QB)
                ? _table.Replace(QB, string.Empty).Replace(QE, string.Empty)
                : _table;

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

        public string QuotedFullName =>
            string.IsNullOrWhiteSpace(_schema)
                ? QuotedObjectName
                : QuotedSchemaName + '.' + QuotedObjectName;

        public string UnquotedFullName =>
            string.IsNullOrWhiteSpace(_schema)
                ? UnquotedObjectName
                : UnquotedSchemaName + '.' + UnquotedObjectName;

#pragma warning disable SP3110 // Identifier Spelling
        [Obsolete("Please, use QuotedObjectName instead")]
        public string QuotatedObjectName => QuotedObjectName;

        [Obsolete("Please, use UnquotedObjectName instead")]
        public string UnquotatedObjectName => UnquotedObjectName;

        [Obsolete("Please, use QuotedSchemaName instead")]
        public string QuotatedSchemaName => QuotedSchemaName;

        [Obsolete("Please, use UnquotedSchemaName instead")]
        public string UnquotatedSchemaName => UnquotedSchemaName;

        [Obsolete("Please, use QuotedFullName instead")]
        public string QuotatedFullName => QuotedFullName;

        [Obsolete("Please, use UnquotedFullName instead")]
        public string UnquotatedFullName => UnquotedFullName;
#pragma warning restore SP3110 // Identifier Spelling


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
                    _ => QB
                };
                var endingQuote = QE switch
                {
                    "]" => @"\]",
                    "" => @"""",
                    _ => QB
                };

                //see also: https://stackoverflow.com/questions/60747665/regex-expression-for-parsing-sql-server-schema-and-tablename?noredirect=1#comment107559387_60747665
                return $@"\.? *(?:{beginningQuote}[^{endingQuote}]+{endingQuote}|\w+)"; //Original Regex:  \.? *(?:\[[^]]+\]|\w+)
            }
        }
    }
}

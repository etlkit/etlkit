using System.Linq;
using TSQL;
using TSQL.Statements;
using TSQL.Tokens;

namespace EtlKit.Helper
{
    /// <summary>
    /// Lightweight T-SQL parsing utilities.
    /// </summary>
    [PublicAPI]
    public static class SqlParser
    {
        /// <summary>
        /// Extracts the top-level select-list column names/expressions from a <c>SELECT</c> statement,
        /// splitting on commas outside of parentheses. Returns an empty list if <paramref name="sql"/>
        /// does not parse as a single <c>SELECT</c> statement.
        /// </summary>
        /// <param name="sql">A SQL <c>SELECT</c> statement.</param>
        public static List<string> ParseColumnNames(string sql)
        {
            var result = new List<string>();
            if (
                TSQLStatementReader.ParseStatements(sql).FirstOrDefault()
                is not TSQLSelectStatement statement
            )
                return result;

            var bracesNestingLevel = 0;
            var previousToken = string.Empty;
            foreach (var token in statement.Select.Tokens)
            {
                CheckOpeningAndClosingBraces(token, ref bracesNestingLevel);

                switch (token.Type)
                {
                    case TSQLTokenType.Identifier:
                        previousToken = token.Text;
                        break;
                    case TSQLTokenType.Character when bracesNestingLevel <= 0 && token.Text == ",":
                        result.Add(previousToken);
                        break;
                }
            }
            if (previousToken != string.Empty)
                result.Add(previousToken);
            return result;
        }

        private static void CheckOpeningAndClosingBraces(TSQLToken token, ref int bracesNesting)
        {
            switch (token.Type)
            {
                case TSQLTokenType.Character when token.Text == "(":
                    bracesNesting++;
                    break;
                case TSQLTokenType.Character when token.Text == ")":
                    bracesNesting--;
                    break;
            }
        }
    }
}

using System.Linq;
using TSQL;
using TSQL.Statements;
using TSQL.Tokens;

namespace EtlKit.Helper
{
    [PublicAPI]
    public static class SqlParser
    {
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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EtlKit;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestControlFlowTasks.Fixtures;
using EtlKit.TestShared.SharedFixtures;

namespace EtlKit.TestControlFlowTasks
{
    [Collection(nameof(ControlFlowCollection))]
    public class SqlTaskTests : ControlFlowTestBase
    {
        public SqlTaskTests(ControlFlowDatabaseFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public static IEnumerable<object[]> ConnectionsWithValue(string value) =>
            new[]
            {
                new object[] { ClickHouseConnection, value },
                new object[] { SqlConnection, value },
                new object[] { PostgresConnection, value },
                new object[] { MySqlConnection, value },
                //new object[] { SqliteConnection, value }
            };

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteNonQuery(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture tc = new TwoColumnsTableFixture(connection, "NonQueryTest");

            //Act
            SqlTask.ExecuteNonQuery(
                connection,
                "Test insert with parameter",
                $@"INSERT INTO {tc.QB}NonQueryTest{tc.QE} VALUES (1, 'Test1')"
            );

            //Assert
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "NonQueryTest",
                    $@"{tc.QB}Col1{tc.QE} = 1 AND {tc.QB}Col2{tc.QE}='Test1'"
                )
            );
        }

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteNonQueryWithParameter(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture tc = new TwoColumnsTableFixture(connection, "ParameterTest");

            //Act
            var parameter = new List<QueryParameter>
            {
                new("value1", "INT", 1),
                new("value2", "NVARCHAR(100)", "Test1"),
            };
            SqlTask.ExecuteNonQuery(
                connection,
                "Test insert with parameter",
                $"INSERT INTO {tc.QB}ParameterTest{tc.QE} VALUES (@value1, @value2)",
                parameter
            );

            //Assert
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "ParameterTest",
                    $@"{tc.QB}Col1{tc.QE} = 1 AND {tc.QB}Col2{tc.QE}='Test1'"
                )
            );
        }

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteScalar(IConnectionManager connection)
        {
            //Arrange
            //Act
            object result = SqlTask.ExecuteScalar(
                connection,
                "Test execute scalar",
                @"SELECT 'Hallo Welt' AS ScalarResult"
            );
            //Assert
            Assert.Equal("Hallo Welt", result.ToString());
        }

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteScalarWithCasting(IConnectionManager connection)
        {
            if (connection.GetType() == typeof(SQLiteConnectionManager))
            {
                //Arrange
                //Act
                double? result = SqlTask.ExecuteScalar<double>(
                    connection,
                    "Test execute scalar with datatype",
                    @"SELECT CAST(1.343 AS NUMERIC(4,3)) AS ScalarResult"
                );
                //Assert
                Assert.Equal(1.343, result);
            }
            else
            {
                //Arrange
                //Act
                DateTime result = (DateTime)
                    SqlTask.ExecuteScalar(
                        connection,
                        "Test execute scalar with datatype",
                        @"SELECT CAST('2020-02-29' AS DATE) AS ScalarResult"
                    );
                //Assert
                Assert.Equal(DateTime.Parse("2020-02-29", CultureInfo.InvariantCulture), result);
            }
        }

        [
            Theory,
            MemberData(nameof(ConnectionsWithValue), "1"),
            MemberData(nameof(ConnectionsWithValue), "7"),
            MemberData(nameof(ConnectionsWithValue), "NULL"),
            MemberData(nameof(ConnectionsWithValue), "'true'")
        ]
        public void ExecuteScalarAsBool(IConnectionManager connection, string sqlBoolValue)
        {
            //Arrange
            //Act
            bool result = SqlTask.ExecuteScalarAsBool(
                connection,
                "Test execute scalar as bool",
                $"SELECT {sqlBoolValue} AS Bool"
            );
            //Assert
            if (sqlBoolValue == "NULL")
                Assert.False(result);
            else
                Assert.True(result);
        }

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteReaderSingleColumn(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture tc = new TwoColumnsTableFixture(connection, "ExecuteReader");
            tc.InsertTestData();
            List<int> asIsResult = new List<int>();
            List<int> toBeResult = new List<int> { 1, 2, 3 };

            //Act
            SqlTask.ExecuteReader(
                connection,
                "Test execute reader",
                $"SELECT {tc.QB}Col1{tc.QE} FROM {tc.QB}ExecuteReader{tc.QE}",
                colA => asIsResult.Add(int.Parse(colA.ToString()!))
            );

            //Assert
            Assert.Equal(toBeResult.OrderBy(t => t), asIsResult.OrderBy(t => t));
        }

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteReaderWithParameter(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture tc = new TwoColumnsTableFixture(
                connection,
                "ExecuteReaderWithPar"
            );
            tc.InsertTestData();
            List<int> asIsResult = new List<int>();
            List<int> toBeResult = new List<int> { 2 };

            List<QueryParameter> parameter = new List<QueryParameter>
            {
                new("par1", "NVARCHAR(10)", "Test2"),
            };
            //Act
            SqlTask.ExecuteReader(
                connection,
                "Test execute reader",
                $"SELECT {tc.QB}Col1{tc.QE} FROM {tc.QB}ExecuteReaderWithPar{tc.QE} WHERE {tc.QB}Col2{tc.QE} = @par1",
                parameter,
                colA => asIsResult.Add(int.Parse(colA.ToString()!))
            );
            //Assert
            Assert.Equal(toBeResult, asIsResult);
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public sealed class MySimpleRow : IEquatable<MySimpleRow>
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }

            public MySimpleRow() { }

            public MySimpleRow(int col1, string col2)
            {
                Col1 = col1;
                Col2 = col2;
            }

            public bool Equals(MySimpleRow other) =>
                other != null && other.Col1 == Col1 && other.Col2 == Col2;

            public override bool Equals(object obj)
            {
                return Equals((MySimpleRow)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Col1, Col2);
            }
        }

        [Theory, MemberData(nameof(Connections))]
        public void ExecuteReaderMultiColumn(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture tc = new TwoColumnsTableFixture(connection, "MultiColumnRead");
            tc.InsertTestData();

            List<MySimpleRow> asIsResult = new List<MySimpleRow>();
            List<MySimpleRow> toBeResult = new List<MySimpleRow>
            {
                new(1, "Test1"),
                new(2, "Test2"),
                new(3, "Test3"),
            };
            MySimpleRow CurColumn = new MySimpleRow();

            //Act
            SqlTask.ExecuteReader(
                connection,
                "Test execute reader",
                $"SELECT * FROM {tc.QB}MultiColumnRead{tc.QE}",
                () => CurColumn = new MySimpleRow(),
                () => asIsResult.Add(CurColumn),
                colA => CurColumn.Col1 = int.Parse(colA.ToString()!),
                colB => CurColumn.Col2 = (string)colB
            );

            //Assert
            Assert.Equal(toBeResult.OrderBy(t => t.Col1), asIsResult.OrderBy(t => t.Col1));
        }
    }
}

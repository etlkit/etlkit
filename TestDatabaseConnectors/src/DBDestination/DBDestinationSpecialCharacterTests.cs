using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationSpecialCharacterTests : DatabaseConnectorsTestBase
    {
        public DbDestinationSpecialCharacterTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> SqlConnections => AllSqlConnections;

        private static void InsertTestData(IConnectionManager connection, string tableName)
        {
            var tn = new ObjectNameDescriptor(tableName, connection.QB, connection.QE);

            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'\0 \"" \b \n \r \t \Z \\ \% \_ ')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(2,' '' """" ')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(3,' !""�$%&/())='' ')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(4,NULL)"
            );
        }

        [Theory, MemberData(nameof(AllOdbcConnections)), MemberData(nameof(SqlConnections))]
        public void ColumnMapping(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(
                connection,
                "SpecialCharacterSource"
            );
            InsertTestData(connection, "SpecialCharacterSource");

            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                connection,
                "SpecialCharacterDestination"
            );

            //Act
            DbSource<string[]> source = new DbSource<string[]>
            {
                ConnectionManager = connection,
                SourceTableDefinition = s2C.TableDefinition,
            };
            DbDestination<string[]> dest = new DbDestination<string[]>
            {
                ConnectionManager = connection,
                DestinationTableDefinition = d2C.TableDefinition,
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(4, RowCountTask.Count(connection, "SpecialCharacterDestination"));
        }
    }
}

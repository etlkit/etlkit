using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBSource
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbSourceWithSqlTests : DatabaseConnectorsTestBase
    {
        public DbSourceWithSqlTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MySimpleRow
        {
            public long Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public void SqlWithSelectStar(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(connection, "SourceSelectStar");
            s2C.InsertTestData();
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                connection,
                "DestinationSelectStar"
            );

            //Act
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>
            {
                Sql = $@"SELECT * FROM {s2C.QB}SourceSelectStar{s2C.QE}",
                ConnectionManager = connection,
            };
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                connection,
                "DestinationSelectStar"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();
            d2C.AssertTestData();
            //Assert
        }

        [Theory, MemberData(nameof(Connections))]
        public void SqlWithNamedColumns(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(connection, "SourceSql");
            s2C.InsertTestData();
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(connection, "DestinationSql");

            //Act
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>
            {
                Sql =
                    $@"SELECT CASE WHEN {s2C.QB}Col1{s2C.QE} IS NOT NULL THEN {s2C.QB}Col1{s2C.QE} ELSE {s2C.QB}Col1{s2C.QE} END AS {s2C.QB}Col1{s2C.QE}, 
{s2C.QB}Col2{s2C.QE} 
FROM {s2C.QB}SourceSql{s2C.QE}",
                ConnectionManager = connection,
            };
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                connection,
                "DestinationSql"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            d2C.AssertTestData();
        }
    }
}

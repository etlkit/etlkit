using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationTests : DatabaseConnectorsTestBase
    {
        public DbDestinationTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MyExtendedRow
        {
            [ColumnMap("Col1")]
            public int Id { get; set; }

            [ColumnMap("Col3")]
            public long? Value { get; set; }

            [ColumnMap("Col4")]
            public decimal Percentage { get; set; }

            [ColumnMap("Col2")]
            public string Text { get; set; }
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void ColumnMapping(IConnectionManager connection)
        {
            //Arrange
            FourColumnsTableFixture source4Columns = new FourColumnsTableFixture(
                connection,
                "Source"
            );
            source4Columns.InsertTestData();
            FourColumnsTableFixture dest4Columns = new FourColumnsTableFixture(
                connection,
                "Destination",
                identityColumnIndex: IsIdentitySupported(connection) ? 2 : -1
            );

            DbSource<string[]> source = new DbSource<string[]>(connection, "Source");
            RowTransformation<string[], MyExtendedRow> trans = new RowTransformation<
                string[],
                MyExtendedRow
            >(row => new MyExtendedRow
            {
                Id = int.Parse(row[0]),
                Text = row[1],
                Value = row[2] != null ? long.Parse(row[2]) : null,
                Percentage = decimal.Parse(row[3]),
            });

            //Act
            DbDestination<MyExtendedRow> dest = new DbDestination<MyExtendedRow>(
                connection,
                "Destination"
            );
            source.LinkTo(trans);
            trans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest4Columns.AssertTestData();
        }
    }
}

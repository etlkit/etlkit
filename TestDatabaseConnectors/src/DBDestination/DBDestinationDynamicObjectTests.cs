using System.Dynamic;
using System.Threading;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationDynamicObjectTests : DatabaseConnectorsTestBase
    {
        public DbDestinationDynamicObjectTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void SourceMoreColumnsThanDestination(IConnectionManager connection)
        {
            //Arrange
            var source4Columns = new FourColumnsTableFixture(connection, "SourceDynamic4Cols");
            source4Columns.InsertTestData();
            var dest2Columns = new TwoColumnsTableFixture(connection, "DestinationDynamic2Cols");

            //Act
            var source = new DbSource<ExpandoObject>(connection, "SourceDynamic4Cols");
            var dest = new DbDestination<ExpandoObject>(connection, "DestinationDynamic2Cols");

            source.LinkTo(dest);
            source.Execute(CancellationToken.None);
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        [Theory]
        [MemberData(nameof(AllSqlConnections))]
        public void DestinationMoreColumnsThanSource(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "DestinationDynamicDiffCols");
            var source2Columns = new TwoColumnsTableFixture(connection, "SourceDynamicDiffCols");
            source2Columns.InsertTestData();
            CreateTableTask.Create(
                connection,
                "DestinationDynamicDiffCols",
                new List<TableColumn>
                {
                    new("Id", "int") { DefaultValue = "0" },
                    new("Col5", "VARCHAR(100)", true),
                    new("Col2", "VARCHAR(100)", true),
                    new("Col1", "INT", true),
                    new("ColX", "INT", true),
                }
            );

            //Act
            var source = new DbSource<ExpandoObject>(connection, "SourceDynamicDiffCols");
            var dest = new DbDestination<ExpandoObject>(connection, "DestinationDynamicDiffCols");

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            var qb = connection.QB;
            var qe = connection.QE;
            Assert.Equal(3, RowCountTask.Count(connection, "DestinationDynamicDiffCols"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DestinationDynamicDiffCols",
                    $"{qb}Col1{qe} = 1 AND {qb}Col2{qe}='Test1' AND {qb}Col5{qe} IS NULL AND {qb}ColX{qe} IS NULL"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DestinationDynamicDiffCols",
                    $"{qb}Col1{qe} = 2 AND {qb}Col2{qe}='Test2' AND {qb}Col5{qe} IS NULL AND {qb}ColX{qe} IS NULL"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DestinationDynamicDiffCols",
                    $"{qb}Col1{qe} = 3 AND {qb}Col2{qe}='Test3' AND {qb}Col5{qe} IS NULL AND {qb}ColX{qe} IS NULL"
                )
            );
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutSQLite))]
        public void DestinationDifferentColumnSizeThanSource(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "DestinationDynamicDiffCols");
            var source2Columns = new TwoColumnsTableFixture(connection, "SourceDynamicDiffCols");
            source2Columns.InsertTestData();
            CreateTableTask.Create(
                connection,
                "DestinationDynamicDiffCols",
                new List<TableColumn>
                {
                    new("Id", "int") { DefaultValue = "0" },
                    new("Col5", "VARCHAR(4)", true),
                    new("Col2", "VARCHAR(4)", true),
                    new("Col1", "INT", true),
                    new("ColX", "INT", true),
                }
            );

            //Arrange
            var source = new DbSource<ExpandoObject>(connection, "SourceDynamicDiffCols");
            var dest = new DbDestination<ExpandoObject>(connection, "DestinationDynamicDiffCols");

            source.LinkTo(dest);

            //Act & Assert
            Assert.Throws<AggregateException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void DestinationMoreColumnsThanSource_WithIdentity(IConnectionManager connection)
        {
            //Arrange
            var sourceTable = "SourceDynamicDiffCols2";
            var destTable = "DestinationDynamicDiffCols2";
            DropTableTask.DropIfExists(connection, destTable);
            var source2Columns = new TwoColumnsTableFixture(connection, sourceTable);
            source2Columns.InsertTestData();
            CreateTableTask.Create(
                connection,
                destTable,
                new List<TableColumn>
                {
                    new("Id", "int", false, true, true),
                    new("Col2", "VARCHAR(100)", true),
                    new("Col1", "INT", true),
                }
            );

            //Act
            var source = new DbSource<ExpandoObject>(connection, sourceTable);
            var dest = new DbDestination<ExpandoObject>(connection, destTable);

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            var qb = connection.QB;
            var qe = connection.QE;
            Assert.Equal(3, RowCountTask.Count(connection, destTable));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    destTable,
                    $"{qb}Col1{qe} = 1 AND {qb}Col2{qe} = 'Test1' AND {qb}Id{qe} = 1"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    destTable,
                    $"{qb}Col1{qe} = 2 AND {qb}Col2{qe}='Test2' AND {qb}Id{qe} = 2"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    destTable,
                    $"{qb}Col1{qe} = 3 AND {qb}Col2{qe}='Test3' AND {qb}Id{qe} = 3"
                )
            );
        }

        [Theory]
        [MemberData(nameof(AllConnectionsWithoutSQLite))]
        public void DestinationWithIdentityColumn(IConnectionManager connection)
        {
            //Arrange
            var source2Columns = new TwoColumnsTableFixture(connection, "SourceDynamicIDCol");
            source2Columns.InsertTestData();
            CreateTableTask.Create(
                connection,
                "DestinationDynamicIdCol",
                new List<TableColumn>
                {
                    new("Id", "BIGINT", false, true, true),
                    new("Col2", "VARCHAR(100)", true),
                    new("Col1", "INT", true),
                    new("ColX", "INT", true),
                }
            );

            //Act
            var source = new DbSource<ExpandoObject>(connection, "SourceDynamicIDCol");
            var dest = new DbDestination<ExpandoObject>(connection, "DestinationDynamicIdCol");

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            var qb = connection.QB;
            var qe = connection.QE;
            Assert.Equal(3, RowCountTask.Count(connection, "DestinationDynamicIdCol"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DestinationDynamicIdCol",
                    $"{qb}Col1{qe} = 1 AND {qb}Col2{qe}='Test1' AND {qb}Id{qe} > 0 AND {qb}ColX{qe} IS NULL"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DestinationDynamicIdCol",
                    $"{qb}Col1{qe} = 2 AND {qb}Col2{qe}='Test2' AND {qb}Id{qe} > 0 AND {qb}ColX{qe} IS NULL"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DestinationDynamicIdCol",
                    $"{qb}Col1{qe} = 3 AND {qb}Col2{qe}='Test3' AND {qb}Id{qe} > 0 AND {qb}ColX{qe} IS NULL"
                )
            );
        }
    }
}

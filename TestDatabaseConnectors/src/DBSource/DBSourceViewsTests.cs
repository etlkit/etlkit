using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBSource
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbSourceViewsTests : DatabaseConnectorsTestBase
    {
        public DbSourceViewsTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public void SimpleFlow(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture source2Columns = new TwoColumnsTableFixture(
                connection,
                "dbsource_simple"
            );
            source2Columns.InsertTestData();
            CreateViewTask.CreateOrAlter(
                connection,
                "DbSourceView",
                "SELECT * FROM dbsource_simple"
            );
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                connection,
                "DbDestinationSimple"
            );

            //Act
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>(connection, "DbSourceView");
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                connection,
                "DbDestinationSimple"
            );

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest2Columns.AssertTestData();
        }

        public class MyExtendedRow
        {
            public string Col2 { get; set; }
            public long? Col3 { get; set; }
            public double Col4 { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public void DifferentColumnsInView(IConnectionManager connection)
        {
            //Arrange
            FourColumnsTableFixture s4C = new FourColumnsTableFixture(
                connection,
                "dbsource_extended"
            );
            s4C.InsertTestData();
            CreateViewTask.CreateOrAlter(
                connection,
                "DbSourceViewExtended",
                $"SELECT {s4C.QB}Col2{s4C.QE}, {s4C.QB}Col4{s4C.QE} FROM dbsource_extended"
            );
            FourColumnsTableFixture d4C = new FourColumnsTableFixture(
                connection,
                "DbDestinationExtended",
                IsIdentitySupported(connection) ? 1 : -1
            );

            //Act
            DbSource<MyExtendedRow> source = new DbSource<MyExtendedRow>(
                connection,
                "DbSourceViewExtended"
            );
            DbDestination<MyExtendedRow> dest = new DbDestination<MyExtendedRow>(
                connection,
                "DbDestinationExtended"
            );

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            d4C.AssertTestData();
        }
    }
}

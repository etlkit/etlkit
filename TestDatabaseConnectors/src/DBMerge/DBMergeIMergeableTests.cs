using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBMerge
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbMergeIMergeableTests : DatabaseConnectorsTestBase
    {
        public DbMergeIMergeableTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MySimpleRow : IMergeableRow
        {
            [ColumnMap("Col1")]
            public long Key { get; set; }

            [ColumnMap("Col2")]
            public string Value { get; set; }
            public DateTime ChangeDate { get; set; }
            public ChangeAction? ChangeAction { get; set; }
            public string UniqueId => Key.ToString();
            public static bool IsDeletion => false;
        }

        [Theory, MemberData(nameof(Connections))]
        public void IdColumnOnlyWithGetter(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(connection, "DBMergeSource");
            s2C.InsertTestData();
            s2C.InsertTestDataSet2();
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                connection,
                "DBMergeDestination"
            );
            d2C.InsertTestDataSet3();
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>(connection, "DBMergeSource");

            //Act
            DbMerge<MySimpleRow> dest = new DbMerge<MySimpleRow>(connection, "DBMergeDestination");
            dest.MergeProperties.IdPropertyNames.Add("UniqueId");
            dest.UseTruncateMethod = true;
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.True(dest.UseTruncateMethod);
            Assert.Equal(
                6,
                RowCountTask.Count(
                    connection,
                    "DBMergeDestination",
                    $"{d2C.QB}Col1{d2C.QE} BETWEEN 1 AND 7 AND {d2C.QB}Col2{d2C.QE} LIKE 'Test%'"
                )
            );
            Assert.True(dest.DeltaTable.Count == 7);
            Assert.True(dest.DeltaTable.Count(row => row.ChangeAction == ChangeAction.Update) == 3);
            Assert.True(
                dest.DeltaTable.Count(row =>
                    row.ChangeAction == ChangeAction.Delete && row.Key == 10
                ) == 1
            );
            Assert.True(dest.DeltaTable.Count(row => row.ChangeAction == ChangeAction.Insert) == 3);
        }

        [Theory, MemberData(nameof(Connections))]
        public void WithDeltaDestinationAndTruncate(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(connection, "DBMergeSource");
            s2C.InsertTestData();
            s2C.InsertTestDataSet2();
            TwoColumnsTableFixture d2C = new TwoColumnsTableFixture(
                connection,
                "DBMergeDestination"
            );
            d2C.InsertTestDataSet3();
            var _ = new TwoColumnsDeltaTableFixture(connection, "DBMergeDelta");

            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>(connection, "DBMergeSource");

            //Act
            DbMerge<MySimpleRow> merge = new DbMerge<MySimpleRow>(connection, "DBMergeDestination");
            merge.MergeProperties.IdPropertyNames.Add("UniqueId");
            merge.UseTruncateMethod = true;
            DbDestination<MySimpleRow> delta = new DbDestination<MySimpleRow>(
                connection,
                "DBMergeDelta"
            );

            source.LinkTo(merge);
            merge.LinkTo(delta);
            source.Execute();
            merge.Wait();
            delta.Wait();

            //Assert
            Assert.True(merge.UseTruncateMethod);
            Assert.Equal(
                6,
                RowCountTask.Count(
                    connection,
                    "DBMergeDestination",
                    $"{d2C.QB}Col1{d2C.QE} BETWEEN 1 AND 7 AND {d2C.QB}Col2{d2C.QE} LIKE 'Test%'"
                )
            );
            Assert.Equal(
                7,
                RowCountTask.Count(
                    connection,
                    "DBMergeDelta",
                    $"{d2C.QB}Col1{d2C.QE} BETWEEN 1 AND 10 AND {d2C.QB}Col2{d2C.QE} LIKE 'Test%'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DBMergeDelta",
                    $"{d2C.QB}ChangeAction{d2C.QE} = '3' AND {d2C.QB}Col1{d2C.QE} = 10"
                )
            );
            Assert.Equal(
                3,
                RowCountTask.Count(
                    connection,
                    "DBMergeDelta",
                    $"{d2C.QB}ChangeAction{d2C.QE} = '2' AND {d2C.QB}Col1{d2C.QE} IN (1,2,4)"
                )
            );
            Assert.Equal(
                3,
                RowCountTask.Count(
                    connection,
                    "DBMergeDelta",
                    $"{d2C.QB}ChangeAction{d2C.QE} = '1' AND {d2C.QB}Col1{d2C.QE} IN (3,5,6)"
                )
            );
        }
    }
}

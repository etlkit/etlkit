using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBMerge
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbMergeCompositeKeysTests : DatabaseConnectorsTestBase
    {
        public DbMergeCompositeKeysTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MyMergeRow : MergeableRow
        {
            [IdColumn]
            public long ColKey1 { get; set; }

            [IdColumn]
            public string ColKey2 { get; set; }

            [CompareColumn]
            public string ColValue1 { get; set; }

            [CompareColumn]
            public string ColValue2 { get; set; }
        }

        private static void ReCreateTable(IConnectionManager connection, ObjectNameDescriptor tn)
        {
            DropTableTask.DropIfExists(connection, tn.ObjectName);

            CreateTableTask.Create(
                connection,
                tn.ObjectName,
                new List<TableColumn>
                {
                    new("ColKey1", "INT", allowNulls: false, isPrimaryKey: true),
                    new("ColKey2", "CHAR(1)", allowNulls: false, isPrimaryKey: true),
                    new("ColValue1", "NVARCHAR(100)", allowNulls: true, isPrimaryKey: false),
                    new("ColValue2", "NVARCHAR(100)", allowNulls: true, isPrimaryKey: false),
                }
            );
        }

        private static void InsertSourceData(IConnectionManager connection, ObjectNameDescriptor tn)
        {
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'I','Insert', 'Test1')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'U','Update', 'Test2')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'E','NoChange', 'Test3')"
            );
        }

        private static void InsertDestinationData(
            IConnectionManager connection,
            ObjectNameDescriptor tn
        )
        {
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'U','Update', 'XXX')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'E','NoChange', 'Test3')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1,'D','Delete', 'Test4')"
            );
        }

        [Theory, MemberData(nameof(Connections))]
        public void MergeWithCompositeKey(IConnectionManager connection)
        {
            //Arrange
            ObjectNameDescriptor sourceTn = new ObjectNameDescriptor(
                "DBMergeSource",
                connection.QB,
                connection.QE
            );
            ObjectNameDescriptor destinationTn = new ObjectNameDescriptor(
                "DBMergeDestination",
                connection.QB,
                connection.QE
            );
            ReCreateTable(connection, sourceTn);
            ReCreateTable(connection, destinationTn);
            InsertSourceData(connection, sourceTn);
            InsertDestinationData(connection, destinationTn);
            //Act
            DbSource<MyMergeRow> source = new DbSource<MyMergeRow>(connection, "DBMergeSource");
            DbMerge<MyMergeRow> dest = new DbMerge<MyMergeRow>(connection, "DBMergeDestination");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(connection, "DBMergeDestination"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DBMergeDestination",
                    $"{destinationTn.QB}ColKey2{destinationTn.QE} = 'E' and {destinationTn.QB}ColValue2{destinationTn.QE} = 'Test3'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DBMergeDestination",
                    $"{destinationTn.QB}ColKey2{destinationTn.QE} = 'U' and {destinationTn.QB}ColValue2{destinationTn.QE} = 'Test2'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    connection,
                    "DBMergeDestination",
                    $"{destinationTn.QB}ColKey2{destinationTn.QE} = 'I' and {destinationTn.QB}ColValue2{destinationTn.QE} = 'Test1'"
                )
            );
        }
    }
}

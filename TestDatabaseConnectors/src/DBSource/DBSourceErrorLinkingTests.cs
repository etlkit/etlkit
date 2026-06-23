using System.Threading;
using System.Threading.Tasks;
using EtlKit;
using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBSource
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbSourceErrorLinkingTests : DatabaseConnectorsTestBase
    {
        public DbSourceErrorLinkingTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }

            [ColumnMap("Col3")]
            public int AddCol { get; set; }
        }

        [Theory, MemberData(nameof(Connections))]
        public async Task RedirectErrorWithObject(IConnectionManager connection)
        {
            if (connection.GetType() == typeof(SQLiteConnectionManager))
            {
                await Task.Delay(100); //Database was locked and needs to recover after exception
            }

            //Arrange
            CreateSourceTable(connection, "DbSourceErrorLinking");
            TwoColumnsTableFixture dest2Columns = new TwoColumnsTableFixture(
                connection,
                "DbDestinationErrorLinking"
            );

            //Act
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>(
                connection,
                "DbSourceErrorLinking"
            );
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                connection,
                "DbDestinationErrorLinking"
            );
            MemoryDestination<EtlKitError> errorDest = new MemoryDestination<EtlKitError>();
            source.LinkTo(dest);
            source.LinkErrorTo(errorDest);
            await source.ExecuteAsync(CancellationToken.None);
            await dest.Completion.ConfigureAwait(true);
            await errorDest.Completion.ConfigureAwait(true);

            //Assert
            dest2Columns.AssertTestData();
            Assert.Collection(
                errorDest.Data,
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    ),
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    ),
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    )
            );
        }

        [Theory, MemberData(nameof(Connections))]
        public void WithoutErrorLinking(IConnectionManager connection)
        {
            //Arrange
            CreateSourceTable(connection, "DbSourceNoErrorLinking");

            var _ = new TwoColumnsTableFixture(connection, "DbDestinationNoErrorLinking");

            //Act
            DbSource<MySimpleRow> source = new DbSource<MySimpleRow>(
                connection,
                "DbSourceNoErrorLinking"
            );
            DbDestination<MySimpleRow> dest = new DbDestination<MySimpleRow>(
                connection,
                "DbDestinationNoErrorLinking"
            );
            source.LinkTo(dest);

            //Assert
            Assert.Throws<FormatException>(() =>
            {
                source.Execute(CancellationToken.None);
                dest.Wait();
            });
        }

        private static void CreateSourceTable(IConnectionManager connection, string tableName)
        {
            DropTableTask.DropIfExists(connection, tableName);

            var tableDefinition = new TableDefinition(
                tableName,
                new List<TableColumn>
                {
                    new("Id", "INT", allowNulls: false, true),
                    new("Col1", "VARCHAR(100)", allowNulls: true),
                    new("Col2", "VARCHAR(100)", allowNulls: true),
                    new("Col3", "VARCHAR(100)", allowNulls: true),
                }
            );
            tableDefinition.CreateTable(connection);
            ObjectNameDescriptor tn = new ObjectNameDescriptor(
                tableName,
                connection.QB,
                connection.QE
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1, '1','Test1','1')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(2, '1.35','TestX','X')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(3, '2','Test2', NULL)"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(4, 'X',NULL, NULL)"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(5, '3','Test3', '3')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(6, '4','Test4', 'X')"
            );
        }
    }
}

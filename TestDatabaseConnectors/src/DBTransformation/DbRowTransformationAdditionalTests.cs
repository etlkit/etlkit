using System.Data;
using System.Dynamic;
using System.Threading;
using EtlKit;
using EtlKit.Common.DataFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBTransformation
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbRowTransformationAdditionalTests : DatabaseConnectorsTestBase
    {
        public DbRowTransformationAdditionalTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public class MyRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void ThrowsWithoutTableNameAndDefinition(IConnectionManager connection)
        {
            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                }
            );
            var trans = new RowTransformation<MyRow>(r => r);
            var dbTrans = new DbRowTransformation<MyRow> { ConnectionManager = connection };
            var dest = new MemoryDestination<MyRow>();

            source.LinkTo(trans);
            trans.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);

            Assert.ThrowsAny<Exception>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void UsesPresetDestinationTableDefinition(IConnectionManager connection)
        {
            var destTable = new TwoColumnsTableFixture(connection, "DbRowTransPresetDef");
            var td = TableDefinition.GetDefinitionFromTableName(connection, "DbRowTransPresetDef");

            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "Test1" },
                    new() { Col1 = 2, Col2 = "Test2" },
                    new() { Col1 = 3, Col2 = "Test3" },
                }
            );
            var dbTrans = new DbRowTransformation<MyRow>
            {
                ConnectionManager = connection,
                DestinationTableDefinition = td,
            };
            var dest = new MemoryDestination<MyRow>();

            source.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            destTable.AssertTestData();
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void ErrorWithoutErrorLinkIsThrown(IConnectionManager connection)
        {
            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "X" },
                }
            );
            var dbTrans = new DbRowTransformation<MyRow>(connection, "NonExisting_Table_For_Error");
            var dest = new MemoryDestination<MyRow>();

            source.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);

            Assert.ThrowsAny<Exception>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutClickHouse))]
        public void ErrorIsRoutedToErrorBuffer(IConnectionManager connection)
        {
            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "X" },
                }
            );
            var dbTrans = new DbRowTransformation<MyRow>(connection, "NonExisting_Table_For_Error");
            var dest = new MemoryDestination<MyRow>();
            var errorDest = new MemoryDestination<EtlKitError>();

            source.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);
            dbTrans.LinkErrorTo(errorDest);

            source.Execute(CancellationToken.None);
            dest.Wait();
            errorDest.Wait();

            Assert.Empty(dest.Data);
            Assert.NotEmpty(errorDest.Data);
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void WorksWithLeaveOpenTrueAndFalse(IConnectionManager connection)
        {
            _ = new TwoColumnsTableFixture(connection, "DbRowTransLeaveOpen");

            connection.LeaveOpen = false;
            var sourceA = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                }
            );
            var dbTransA = new DbRowTransformation<MyRow>(connection, "DbRowTransLeaveOpen");
            var destA = new MemoryDestination<MyRow>();
            sourceA.LinkTo(dbTransA);
            dbTransA.LinkTo(destA);
            sourceA.Execute();
            destA.Wait();

            connection.LeaveOpen = true;
            var sourceB = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 2, Col2 = "B" },
                }
            );
            var dbTransB = new DbRowTransformation<MyRow>(connection, "DbRowTransLeaveOpen");
            var destB = new MemoryDestination<MyRow>();
            sourceB.LinkTo(dbTransB);
            dbTransB.LinkTo(destB);
            sourceB.Execute();
            destB.Wait();

            Assert.Equal(2, destA.Data.Count + destB.Data.Count);
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void ArrayInputIsInserted(IConnectionManager connection)
        {
            var table = new TwoColumnsTableFixture(connection, "DbRowTransArray");
            var source = new MemorySource<object[]>(
                new List<object[]>
                {
                    new object[] { 1, "Test1" },
                    new object[] { 2, "Test2" },
                    new object[] { 3, "Test3" },
                }
            );

            var dbTrans = new DbRowTransformation<object[]>(connection, "DbRowTransArray");
            var dest = new MemoryDestination<object[]>();

            source.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            table.AssertTestData();
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void DynamicColumnsExpandOnceAndReuse(IConnectionManager connection)
        {
            var table = new TwoColumnsTableFixture(connection, "DbRowTransDynCols");
            var source = new MemorySource<ExpandoObject> { DataAsList = new List<ExpandoObject>() };
            dynamic r1 = new ExpandoObject();
            r1.Col1 = 1;
            r1.Col2 = "Test1";
            dynamic r2 = new ExpandoObject();
            r2.Col1 = 2;
            r2.Col2 = "Test2";
            dynamic r3 = new ExpandoObject();
            r3.Col1 = 3;
            r3.Col2 = "Test3";
            source.DataAsList.Add(r1);
            source.DataAsList.Add(r2);
            source.DataAsList.Add(r3);

            var dbTrans = new DbRowTransformation<ExpandoObject>(connection, "DbRowTransDynCols");
            var dest = new MemoryDestination<ExpandoObject>();

            source.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            table.AssertTestData();
        }

        [Fact]
        public void TransformationCallsPrepareBulkAndCleanupInOrder()
        {
            var rec = new RecordingConnectionManager();
            var td = new TableDefinition("X", [new("Col1", "INT"), new("Col2", "NVARCHAR(100)")]);

            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                    new() { Col1 = 2, Col2 = "B" },
                }
            );
            var dbTrans = new DbRowTransformation<MyRow>
            {
                ConnectionManager = rec,
                DestinationTableDefinition = td,
            };
            var dest = new MemoryDestination<MyRow>();

            source.LinkTo(dbTrans);
            dbTrans.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Expect sequence per row: Prepare, Before, Bulk, After, CleanUp
            Assert.Equal(10, rec.Calls.Count);
            for (var i = 0; i < 2; i++)
            {
                var offset = i * 5;
                Assert.Equal("Prepare", rec.Calls[offset + 0]);
                Assert.Equal("Before", rec.Calls[offset + 1]);
                Assert.Equal("Bulk", rec.Calls[offset + 2]);
                Assert.Equal("After", rec.Calls[offset + 3]);
                Assert.Equal("CleanUp", rec.Calls[offset + 4]);
            }
        }

        private sealed class RecordingConnectionManager : IConnectionManager
        {
            public readonly List<string> Calls = new();
            public ConnectionManagerType ConnectionManagerType => ConnectionManagerType.SqlServer;
            public IDbConnectionString ConnectionString { get; set; }
            public bool LeaveOpen { get; set; } = true;
            public bool IsInBulkInsert { get; set; }
            public IDbTransaction Transaction { get; set; }
            public ConnectionState? State => ConnectionState.Open;
            public string QB => string.Empty;
            public string QE => string.Empty;
            public bool SupportDatabases => false;
            public bool SupportProcedures => false;
            public bool SupportSchemas => false;
            public bool SupportComputedColumns => false;
            public CultureInfo ConnectionCulture => CultureInfo.InvariantCulture;
            public int MaxLoginAttempts { get; set; }

            public void Open() { }

            public void Close() { }

            public void CloseIfAllowed() { }

            public void Dispose() { }

            public IDbCommand CreateCommand(
                string commandText,
                IEnumerable<IQueryParameter> parameterList
            ) => null;

            public int ExecuteNonQuery(
                string command,
                IEnumerable<IQueryParameter> parameterList = null
            ) => 0;

            public object ExecuteScalar(
                string command,
                IEnumerable<IQueryParameter> parameterList = null
            ) => null;

            public IDataReader ExecuteReader(
                string command,
                IEnumerable<IQueryParameter> parameterList = null
            ) => null;

            public void PrepareBulkInsert(string tableName)
            {
                Calls.Add("Prepare");
            }

            public void CleanUpBulkInsert(string tableName)
            {
                Calls.Add("CleanUp");
            }

            public void BeforeBulkInsert(string tableName)
            {
                Calls.Add("Before");
            }

            public void AfterBulkInsert(string tableName)
            {
                Calls.Add("After");
            }

            public void BulkInsert(ITableData data, string tableName)
            {
                Calls.Add("Bulk");
            }

            public void BeginTransaction(IsolationLevel isolationLevel) { }

            public void BeginTransaction() { }

            public void CommitTransaction() { }

            public void RollbackTransaction() { }

            public void CloseTransaction() { }

            public bool IndexExists(ITask callingTask, string sql) => false;

            public IConnectionManager Clone() => new RecordingConnectionManager();

            public IConnectionManager CloneIfAllowed() => this;
        }
    }
}

using System.Data;
using System.Dynamic;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationAdditionalTests : DatabaseConnectorsTestBase
    {
        public DbDestinationAdditionalTests(DatabaseSourceDestinationFixture fixture)
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
            var dest = new DbDestination<MyRow> { ConnectionManager = connection };

            source.LinkTo(dest);

            Assert.ThrowsAny<Exception>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void UsesPresetDestinationTableDefinition(IConnectionManager connection)
        {
            var destTable = new TwoColumnsTableFixture(connection, "DbDestPresetDef");
            var td = TableDefinition.GetDefinitionFromTableName(connection, "DbDestPresetDef");

            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "Test1" },
                    new() { Col1 = 2, Col2 = "Test2" },
                    new() { Col1 = 3, Col2 = "Test3" },
                }
            );
            var dest = new DbDestination<MyRow>
            {
                ConnectionManager = connection,
                DestinationTableDefinition = td,
            };

            source.LinkTo(dest);
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
            var dest = new DbDestination<MyRow>(connection, "NonExisting_Table_For_Error");

            source.LinkTo(dest);

            Assert.ThrowsAny<Exception>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void WorksWithLeaveOpenTrueAndFalse(IConnectionManager connection)
        {
            _ = new TwoColumnsTableFixture(connection, "DbDestLeaveOpen");

            connection.LeaveOpen = false;
            var sourceA = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                }
            );
            var destA = new DbDestination<MyRow>(connection, "DbDestLeaveOpen");
            sourceA.LinkTo(destA);
            sourceA.Execute();
            destA.Wait();

            connection.LeaveOpen = true;
            var sourceB = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 2, Col2 = "B" },
                }
            );
            var destB = new DbDestination<MyRow>(connection, "DbDestLeaveOpen");
            sourceB.LinkTo(destB);
            sourceB.Execute();
            destB.Wait();

            // Verify both rows were inserted
            Assert.Equal(2, RowCountTask.Count(connection, "DbDestLeaveOpen"));
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void ArrayInputIsInserted(IConnectionManager connection)
        {
            var table = new TwoColumnsTableFixture(connection, "DbDestArray");
            var source = new MemorySource<object[]>(
                new List<object[]>
                {
                    new object[] { 1, "Test1" },
                    new object[] { 2, "Test2" },
                    new object[] { 3, "Test3" },
                }
            );

            var dest = new DbDestination<object[]>(connection, "DbDestArray");

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            table.AssertTestData();
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void DynamicColumnsExpandOnceAndReuse(IConnectionManager connection)
        {
            var table = new TwoColumnsTableFixture(connection, "DbDestDynCols");
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

            var dest = new DbDestination<ExpandoObject>(connection, "DbDestDynCols");

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            table.AssertTestData();
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void BatchSizeAffectsBulkInsertBehavior(IConnectionManager connection)
        {
            var table = new TwoColumnsTableFixture(connection, "DbDestBatchSize");
            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "Test1" },
                    new() { Col1 = 2, Col2 = "Test2" },
                    new() { Col1 = 3, Col2 = "Test3" },
                    new() { Col1 = 4, Col2 = "Test4" },
                    new() { Col1 = 5, Col2 = "Test5" },
                }
            );

            var dest = new DbDestination<MyRow>(connection, "DbDestBatchSize")
            {
                BatchSize =
                    2 // Force multiple batches
                ,
            };

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Verify all 5 rows were inserted
            Assert.Equal(5, RowCountTask.Count(connection, "DbDestBatchSize"));
        }

        [Theory, MemberData(nameof(AllSqlConnections))]
        public void NullRowsAreSkipped(IConnectionManager connection)
        {
            _ = new TwoColumnsTableFixture(connection, "DbDestNullSkip");
            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                    null,
                    new() { Col1 = 2, Col2 = "B" },
                }
            );

            var dest = new DbDestination<MyRow>(connection, "DbDestNullSkip");

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Should have 2 rows, not 3
            Assert.Equal(2, RowCountTask.Count(connection, "DbDestNullSkip"));
        }

        [Fact]
        public void DestinationCallsPrepareBulkAndCleanupWithSingleBatch()
        {
            var rec = new RecordingConnectionManager();
            var td = new TableDefinition(
                "X",
                [new TableColumn("Col1", "INT"), new TableColumn("Col2", "NVARCHAR(100)")]
            );

            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                    new() { Col1 = 2, Col2 = "B" },
                }
            );
            var dest = new DbDestination<MyRow>
            {
                ConnectionManager = rec,
                DestinationTableDefinition = td,
                BatchSize =
                    10 // Single batch for both rows
                ,
            };

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Expect single batch: Prepare, Before, Bulk, After, CleanUp
            // But actual behavior shows 5 calls: Prepare, Before, Bulk, After, CleanUp
            Assert.Equal(5, rec.Calls.Count);
            Assert.Equal("Prepare", rec.Calls[0]);
            Assert.Equal("Before", rec.Calls[1]);
            Assert.Equal("Bulk", rec.Calls[2]);
            Assert.Equal("After", rec.Calls[3]);
            Assert.Equal("CleanUp", rec.Calls[4]);
        }

        [Fact]
        public void DestinationCallsPrepareBulkAndCleanupInOrder()
        {
            var rec = new RecordingConnectionManager();
            var td = new TableDefinition(
                "X",
                [new TableColumn("Col1", "INT"), new TableColumn("Col2", "NVARCHAR(100)")]
            );

            var source = new MemorySource<MyRow>(
                new List<MyRow>
                {
                    new() { Col1 = 1, Col2 = "A" },
                    new() { Col1 = 2, Col2 = "B" },
                }
            );
            var dest = new DbDestination<MyRow>
            {
                ConnectionManager = rec,
                DestinationTableDefinition = td,
                BatchSize =
                    1 // Force separate batches for each row
                ,
            };

            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Expect sequence for 1 single batch: Prepare, Before, Bulk, After, CleanUp
            // With BatchSize=2, we get 2 batches
            // So actual behavior shows 8 calls
            Assert.Equal(8, rec.Calls.Count);

            // Verify the actual sequence: Prepare, Before, Bulk, After
            // Then Before, Bulk, After, CleanUp (One Prepare and one CleanUp for whole sequence)
            Assert.Equal("Prepare", rec.Calls[0]);
            Assert.Equal("Before", rec.Calls[1]);
            Assert.Equal("Bulk", rec.Calls[2]);
            Assert.Equal("After", rec.Calls[3]);
            Assert.Equal("Before", rec.Calls[4]);
            Assert.Equal("Bulk", rec.Calls[5]);
            Assert.Equal("After", rec.Calls[6]);
            Assert.Equal("CleanUp", rec.Calls[7]);
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

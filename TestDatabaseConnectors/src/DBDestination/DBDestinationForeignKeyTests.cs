using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationForeignKeyTests : DatabaseConnectorsTestBase
    {
        public DbDestinationForeignKeyTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public class MyRow
        {
            public long Key1 { get; set; }
            public long Key2 { get; set; }
            public string Value { get; set; }
        }

        private static void ReCreateOtherTable(IConnectionManager connection, string tablename)
        {
            DropTableTask.DropIfExists(connection, tablename);

            CreateTableTask.Create(
                connection,
                tablename,
                new List<TableColumn>
                {
                    new("Id", "INT", allowNulls: false, isPrimaryKey: true),
                    new("Other", "VARCHAR(100)", allowNulls: true, isPrimaryKey: false),
                }
            );
            ObjectNameDescriptor tn = new ObjectNameDescriptor(
                tablename,
                connection.QB,
                connection.QE
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(10,'TestX')"
            );
        }

        private static void ReCreateTable(IConnectionManager connection, string tableName)
        {
            DropTableTask.DropIfExists(connection, tableName);

            CreateTableTask.Create(
                connection,
                tableName,
                new List<TableColumn>
                {
                    new("Key1", "INT", allowNulls: false, isPrimaryKey: true),
                    new("Key2", "INT", allowNulls: false, isPrimaryKey: true),
                    new("Value1", "VARCHAR(100)", allowNulls: true, isPrimaryKey: false),
                }
            );
        }

        private static void AddFkConstraint(
            IConnectionManager connection,
            string sourceTableName,
            string referenceTableName
        )
        {
            ObjectNameDescriptor tn = new ObjectNameDescriptor(
                sourceTableName,
                connection.QB,
                connection.QE
            );
            ObjectNameDescriptor referenceTn = new ObjectNameDescriptor(
                referenceTableName,
                connection.QB,
                connection.QE
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Add FK constraint",
                $@"ALTER TABLE {tn.QuotedFullName}
ADD CONSTRAINT constraint_fk
FOREIGN KEY ({tn.QB}Key2{tn.QE})
REFERENCES {referenceTn.QuotedFullName}({referenceTn.QB}Id{referenceTn.QE})
ON DELETE CASCADE;"
            );
        }

        private static void InsertTestData(IConnectionManager connection, string tableName)
        {
            ObjectNameDescriptor tn = new ObjectNameDescriptor(
                tableName,
                connection.QB,
                connection.QE
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(1, 10 ,'Test1')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(2, 10 ,'Test2')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {tn.QuotedFullName} VALUES(3, 10 ,'Test3')"
            );
        }

        [Theory, MemberData(nameof(AllConnectionsWithoutSQLite))]
        public void WriteIntoTableWithPKAndFk(IConnectionManager connection)
        {
            //Arrange
            ReCreateOtherTable(connection, "FKReferenceTable");
            ReCreateTable(connection, "FKSourceTable");
            ReCreateTable(connection, "FKDestTable");
            InsertTestData(connection, "FKSourceTable");
            AddFkConstraint(connection, "FKDestTable", "FKReferenceTable");

            DbSource<MyRow> source = new DbSource<MyRow>(connection, "FKSourceTable");

            //Act
            DbDestination<MyRow> dest = new DbDestination<MyRow>(connection, "FKDestTable");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(connection, "FKDestTable"));
        }
    }
}

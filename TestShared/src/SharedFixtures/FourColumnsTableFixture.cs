using System.Collections.ObjectModel;
using System.Linq;
using EtlKit;
using EtlKit.ClickHouse.ConnectionManager;
using EtlKit.Common;
using EtlKit.ConnectionManager;
using EtlKit.ControlFlow;
using EtlKit.Primitives;
using EtlKit.TestShared.Helper;

namespace EtlKit.TestShared.SharedFixtures
{
    public class FourColumnsTableFixture
    {
        public IConnectionManager Connection { get; set; } =
            Config.SqlConnection.ConnectionManager("DataFlow");
        public bool IsSQLiteConnection => Connection.GetType() == typeof(SQLiteConnectionManager);
        public bool IsClickHouseConnection =>
            Connection.GetType() == typeof(ClickHouseConnectionManager);

        public TableDefinition TableDefinition { get; set; }
        public string TableName { get; set; }
        public ObjectNameDescriptor TN => new(TableName, Connection.QB, Connection.QE);
        public string QB => Connection.QB;
        public string QE => Connection.QE;

        public FourColumnsTableFixture(string tableName)
        {
            TableName = tableName;
            RecreateTable(0);
        }

        public FourColumnsTableFixture(string tableName, int identityColumnIndex)
        {
            TableName = tableName;
            RecreateTable(identityColumnIndex);
        }

        public FourColumnsTableFixture(IConnectionManager connection, string tableName)
        {
            TableName = tableName;
            Connection = connection;
            RecreateTable(IsClickHouseConnection ? -1 : 0);
        }

        public FourColumnsTableFixture(
            IConnectionManager connection,
            string tableName,
            int identityColumnIndex
        )
        {
            TableName = tableName;
            Connection = connection;
            RecreateTable(identityColumnIndex);
        }

        public void RecreateTable(int identityColumnIndex)
        {
            DropTableTask.DropIfExists(Connection, TableName);
            bool hasIdentityCol = identityColumnIndex >= 0;
            var columns = new ObservableCollection<TableColumn>
            {
                new(
                    "Col1",
                    "INT",
                    allowNulls: IsSQLiteConnection,
                    isPrimaryKey: true,
                    isIdentity: hasIdentityCol
                ),
                new("Col2", "NVARCHAR(100)", allowNulls: true),
                new("Col3", "BIGINT", allowNulls: true),
                new("Col4", "DECIMAL(12,6)", allowNulls: false),
            };
            if (identityColumnIndex > 0)
                columns.Move(0, identityColumnIndex);
            TableDefinition = new TableDefinition(TableName, columns.ToList());
            TableDefinition.CreateTable(Connection);
        }

        public void InsertTestData()
        {
            if (IsSQLiteConnection)
            {
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $"INSERT INTO {TN.QuotedFullName} (Col1, Col2, Col3, Col4) VALUES(NULL, 'Test1', NULL, '1.2')"
                );
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $"INSERT INTO {TN.QuotedFullName} (Col1, Col2, Col3, Col4) VALUES(NULL, 'Test2', 4711, '1.23')"
                );
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $"INSERT INTO {TN.QuotedFullName} (Col1, Col2, Col3, Col4) VALUES(NULL, 'Test3', 185, '1.234')"
                );
            }
            else if (IsClickHouseConnection)
            {
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $@"INSERT INTO {TN.QuotedFullName} VALUES(1, 'Test1', NULL, '1.2')"
                );
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $@"INSERT INTO {TN.QuotedFullName} VALUES(2, 'Test2', 4711, '1.23')"
                );
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $@"INSERT INTO {TN.QuotedFullName}  VALUES(3, 'Test3', 185, '1.234')"
                );
            }
            else
            {
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $@"INSERT INTO {TN.QuotedFullName} ({QB}Col2{QE}, {QB}Col3{QE}, {QB}Col4{QE}) VALUES('Test1', NULL, '1.2')"
                );
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $@"INSERT INTO {TN.QuotedFullName} ({QB}Col2{QE}, {QB}Col3{QE}, {QB}Col4{QE}) VALUES('Test2', 4711, '1.23')"
                );
                SqlTask.ExecuteNonQuery(
                    Connection,
                    "Insert demo data",
                    $@"INSERT INTO {TN.QuotedFullName} ({QB}Col2{QE},{QB}Col3{QE}, {QB}Col4{QE}) VALUES('Test3', 185, '1.234')"
                );
            }
        }

        public void AssertTestData()
        {
            Assert.Equal(3, RowCountTask.Count(Connection, TableName));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    Connection,
                    TableName,
                    $"{QB}Col2{QE} = 'Test1' AND ({QB}Col3{QE} IS NULL OR {QB}Col3{QE} = -1) AND {QB}Col4{QE}='1.2'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    Connection,
                    TableName,
                    $"{QB}Col2{QE} = 'Test2' AND ({QB}Col3{QE} IS NULL OR {QB}Col3{QE} = 4711) AND {QB}Col4{QE}='1.23'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    Connection,
                    TableName,
                    $"{QB}Col2{QE} = 'Test3' AND ({QB}Col3{QE} IS NULL OR {QB}Col3{QE} = 185) AND {QB}Col4{QE}='1.234'"
                )
            );
        }
    }
}

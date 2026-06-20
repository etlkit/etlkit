using System.Threading.Tasks;
using EtlKit;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.Access
{
    public sealed class ImportExportAccessTests : DatabaseConnectorsTestBase
    {
        private readonly TableDefinition _sourceTable;
        private readonly TableDefinition _destinationTable;

        public ImportExportAccessTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture)
        {
            _sourceTable = RecreateAccessTestTable("SourceTable");
            _destinationTable = RecreateAccessTestTable("DestinationTable");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DropTableTask.DropIfExists(AccessOdbcConnection, _sourceTable.Name);
                DropTableTask.DropIfExists(AccessOdbcConnection, _destinationTable.Name);
                AccessOdbcConnection.Close();
            }
            base.Dispose(disposing);
        }

        private TableDefinition RecreateAccessTestTable(string tableName)
        {
            try
            {
                DropTableTask.DropIfExists(AccessOdbcConnection, tableName);
            }
            catch
            {
                // ignored
            }

            TableDefinition testTable = new TableDefinition(
                tableName,
                new List<TableColumn>
                {
                    new("Field1", "NUMBER", allowNulls: true),
                    new("Field2", "CHAR", allowNulls: true),
                }
            );
            new CreateTableTask(testTable)
            {
                ThrowErrorIfTableExists = true,
                ConnectionManager = AccessOdbcConnection,
            }.Execute();
            return testTable;
        }

        //Download and configure Odbc driver for access first! This test points to access file on local path
        //Odbc driver needs to be 64bit if using 64bit .NET core and 32bit if using 32bit version of .NET Core!
        //(Visual Studio 2019 16.4 changed default behaviour for xunit Tests - they now run with .NET Core 32bit versions
        //Driver Access >2016 https://www.microsoft.com/en-us/download/details.aspx?id=54920
        //Driver Access >2010 https://www.microsoft.com/en-us/download/details.aspx?id=13255
        //If LeaveOpen is not set to true, very strange errors may occur:
        //https://stackoverflow.com/questions/37432816/microsoft-ace-oledb-12-0-bug-in-multithread-scenario
        //It is recommended to leave this connection manager always open (this is why leave open is set to true by default)

        [WindowsOnlyFact]
        public void ShallBeLeftOpen()
        {
            Assert.True(AccessOdbcConnection.LeaveOpen); //If LeaveOpen is not set to true, very strange errors may occur
        }

        [WindowsOnlyFact]
        public void CsvIntoAccess()
        {
            //Arrange

            //Act
            CsvSource<string[]> source = new CsvSource<string[]>("res/Access/AccessData.csv");
            DbDestination<string[]> dest = new DbDestination<string[]>(batchSize: 2)
            {
                DestinationTableDefinition = _destinationTable,
                ConnectionManager = AccessOdbcConnection,
            };
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(AccessOdbcConnection, _destinationTable.Name));
        }

        [Serializable]
        public class Data
        {
            [ColumnMap("Col1")]
            public double Field1 { get; set; }
            public string Field2 { get; set; }

            [ColumnMap("Col2")]
            public string Field2Trimmed => Field2.Trim();
        }

        [WindowsOnlyFact]
        public async Task AccessIntoDBWithTableDefinition()
        {
            //Arrange
            await InsertTestData(_sourceTable).ConfigureAwait(false);
            TwoColumnsTableFixture destTable = new TwoColumnsTableFixture(
                SqlConnection,
                "dbo.AccessTargetTableWTD"
            );

            //Act
            DbSource<Data> source = new DbSource<Data>(AccessOdbcConnection)
            {
                SourceTableDefinition = _sourceTable,
            };
            DbDestination<Data> dest = new DbDestination<Data>(
                SqlConnection,
                "dbo.AccessTargetTableWTD"
            );
            source.LinkTo(dest);
            await source.ExecuteAsync().ConfigureAwait(false);
            dest.Wait();

            //Assert
            destTable.AssertTestData();
        }

        private static async Task InsertTestData(TableDefinition table)
        {
            SqlTask.ExecuteNonQueryFormatted(
                AccessOdbcConnection,
                "Insert test data",
                $"INSERT INTO {table.Name:q} (Field1, Field2) VALUES (1,'Test1');"
            );
            SqlTask.ExecuteNonQueryFormatted(
                AccessOdbcConnection,
                "Insert test data",
                $"INSERT INTO {table.Name:q} (Field1, Field2) VALUES (2,'Test2');"
            );
            SqlTask.ExecuteNonQueryFormatted(
                AccessOdbcConnection,
                "Insert test data",
                $"INSERT INTO {table.Name:q} (Field1, Field2) VALUES (3,'Test3');"
            );
            // Below is a workaround for a bug in the Access ODBC driver to allow data to be read
            AccessOdbcConnection.Close();
            await Task.Delay(500).ConfigureAwait(false);
            Assert.Equal(3, RowCountTask.Count(AccessOdbcConnection, table.Name));
        }

        [WindowsOnlyFact]
        public async Task AccessIntoDB()
        {
            //Arrange
            var sourceTable = RecreateAccessTestTable(nameof(AccessIntoDB));
            await InsertTestData(sourceTable).ConfigureAwait(false);
            TwoColumnsTableFixture destTable = new TwoColumnsTableFixture(
                SqlConnection,
                "dbo.AccessTargetTable"
            );

            //Act
            DbSource<Data> source = new DbSource<Data>(AccessOdbcConnection, sourceTable.Name);
            DbDestination<Data> dest = new DbDestination<Data>(
                SqlConnection,
                destTable.TableDefinition.Name
            );
            source.LinkTo(dest);
            await source.ExecuteAsync().ConfigureAwait(false);
            dest.Wait();

            //Assert
            destTable.AssertTestData();
        }
    }
}

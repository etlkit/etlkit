using EtlKit;
using EtlKit.Common;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.LookupTransformation
{
    [Collection("Transformations")]
    public class LookupErrorLinkingTests : TransformationsTestBase
    {
        public LookupErrorLinkingTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Serializable]
        public class MyLookupRow
        {
            [ColumnMap("Col1")]
            public int Key { get; set; }

            [ColumnMap("Col2")]
            public string LookupValue { get; set; }
        }

        [Serializable]
        public class MyInputDataRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void NoErrorLinking()
        {
            //Arrange
            var _ = new TwoColumnsTableFixture(SqlConnection, "LookupErrorLinkingDest");
            CreateSourceTable(SqlConnection, "LookupErrorLinkingSource");
            var lookupSource = new DbSource<MyLookupRow>(SqlConnection, "LookupErrorLinkingSource");

            var source = new MemorySource<MyInputDataRow>
            {
                DataAsList = new List<MyInputDataRow>
                {
                    new() { Col1 = 1 },
                    new() { Col1 = 2 },
                    new() { Col1 = 3 },
                },
            };

            //Act & Assert
            Assert.ThrowsAny<Exception>(() =>
            {
                var lookupTableData = new List<MyLookupRow>();
                var lookup = new LookupTransformation<MyInputDataRow, MyLookupRow>(
                    lookupSource,
                    row =>
                    {
                        row.Col2 = lookupTableData
                            .Where(ld => ld.Key == row.Col1)
                            .Select(ld => ld.LookupValue)
                            .FirstOrDefault();
                        return row;
                    },
                    lookupTableData
                );
                var dest = new DbDestination<MyInputDataRow>(
                    SqlConnection,
                    "LookupErrorLinkingDest"
                );
                source.LinkTo(lookup);
                lookup.LinkTo(dest);
                source.Execute();
                dest.Wait();
            });
        }

        [Fact]
        public void WithObject()
        {
            //Arrange
            var dest2Columns = new TwoColumnsTableFixture(SqlConnection, "LookupErrorLinkingDest");
            CreateSourceTable(SqlConnection, "LookupErrorLinkingSource");
            var lookupSource = new DbSource<MyLookupRow>(SqlConnection, "LookupErrorLinkingSource");

            var source = new MemorySource<MyInputDataRow>
            {
                DataAsList = new List<MyInputDataRow>
                {
                    new() { Col1 = 1 },
                    new() { Col1 = 2 },
                    new() { Col1 = 3 },
                    new() { Col1 = 4 },
                },
            };
            var errorDest = new MemoryDestination<EtlKitError>();

            //Act
            var lookupTableData = new List<MyLookupRow>();
            var lookup = new LookupTransformation<MyInputDataRow, MyLookupRow>(
                lookupSource,
                row =>
                {
                    row.Col2 = lookupTableData
                        .Where(ld => ld.Key == row.Col1)
                        .Select(ld => ld.LookupValue)
                        .FirstOrDefault();
                    if (row.Col1 == 4)
                        throw new Exception("Error record");
                    return row;
                },
                lookupTableData
            );
            var dest = new DbDestination<MyInputDataRow>(SqlConnection, "LookupErrorLinkingDest");
            source.LinkTo(lookup);
            lookup.LinkTo(dest);
            lookup.LinkLookupSourceErrorTo(errorDest);
            lookup.LinkLookupTransformationErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

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
                    )
            );
        }

        private static void CreateSourceTable(IConnectionManager connection, string tableName)
        {
            DropTableTask.DropIfExists(connection, tableName);

            var tableDefinition = new TableDefinition(
                tableName,
                new List<TableColumn>
                {
                    new("Col1", "VARCHAR(100)", allowNulls: true),
                    new("Col2", "VARCHAR(100)", allowNulls: true),
                }
            );
            tableDefinition.CreateTable(connection);
            var TN = new ObjectNameDescriptor(tableName, connection.QB, connection.QE);
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {TN.QuotedFullName} VALUES('1','Test1')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {TN.QuotedFullName} VALUES('2','Test2')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {TN.QuotedFullName} VALUES('X','Test3')"
            );
            SqlTask.ExecuteNonQuery(
                connection,
                "Insert demo data",
                $@"INSERT INTO {TN.QuotedFullName} VALUES('3','Test3')"
            );
        }
    }
}

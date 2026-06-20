using EtlKit;
using EtlKit.Common;
using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.UseCases
{
    [Collection("Transformations")]
    public class UpdateOnHashMatchTests : TransformationsTestBase
    {
        public UpdateOnHashMatchTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        private static void CreateSourceTable(string tableName)
        {
            DropTableTask.DropIfExists(SqlConnection, tableName);
            var sourceTable = new TableDefinition(
                tableName,
                new List<TableColumn>
                {
                    new("id", "INT", allowNulls: false, isPrimaryKey: true, isIdentity: true),
                    new("name", "NVARCHAR(100)", allowNulls: false),
                    new("age", "INT", allowNulls: false),
                }
            );
            sourceTable.CreateTable(SqlConnection);
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Insert demo data",
                $"INSERT INTO {tableName} (name, age) VALUES('Bugs',12)"
            );
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Insert demo data",
                $"INSERT INTO {tableName} (name, age) VALUES('Coyote',8)"
            );
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Insert demo data",
                $"INSERT INTO {tableName} (name, age) VALUES('Pete',19)"
            );
        }

        private static void CreateDestinationTable(string tableName)
        {
            DropTableTask.DropIfExists(SqlConnection, tableName);
            var sourceTable = new TableDefinition(
                tableName,
                new List<TableColumn>
                {
                    new("id", "INT", allowNulls: false, isPrimaryKey: true, isIdentity: true),
                    new("name", "NVARCHAR(100)", allowNulls: false),
                    new("age", "INT", allowNulls: false),
                    new("hashcode", "CHAR(40)", allowNulls: false),
                }
            );
            sourceTable.CreateTable(SqlConnection);
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Insert demo data",
                $"INSERT INTO {tableName} (name, age, hashcode) VALUES('Bugs',12, '{HashHelper.Encrypt_Char40("1Bugs12")}')"
            );
            SqlTask.ExecuteNonQuery(
                SqlConnection,
                "Insert demo data",
                $"INSERT INTO {tableName} (name, age, hashcode) VALUES('Coyote',10, '{HashHelper.Encrypt_Char40("2Coyote10")}')"
            );
        }

        [Fact]
        public void UpdateOnHashMatch()
        {
            //Arrange
            CreateSourceTable("dbo.HashMatchSource");
            CreateDestinationTable("dbo.HashMatchDestination");

            //Act
            var source = new DbSource<string[]>(SqlConnection, "dbo.HashMatchSource");

            var trans = new RowTransformation<string[]>(row =>
            {
                Array.Resize(ref row, row.Length + 1);
                row[^1] = HashHelper.Encrypt_Char40(string.Join("", row));
                return row;
            });

            var allEntriesInDestination = new List<string[]>();
            var lookup = new LookupTransformation<string[], string[]>(
                new DbSource<string[]>(SqlConnection, "dbo.HashMatchDestination"),
                row =>
                {
                    var firstRowClosureCopy = row[0];
                    var matchingIdEntry = allEntriesInDestination.Find(destRow =>
                        destRow[0] == firstRowClosureCopy
                    );
                    if (matchingIdEntry == null)
                        row = null;
                    else if (matchingIdEntry[^1] != row[^1])
                    {
                        SqlTask.ExecuteNonQuery(
                            SqlConnection,
                            "update entry with different hashcode",
                            $@"UPDATE dbo.HashMatchDestination 
                                                  SET name = '{row[1]}',
                                                      age = '{row[2]}',
                                                      hashcode = '{row[3]}'
                                                  WHERE id = {row[0]}
                                                "
                        );
                    }
                    return row;
                },
                allEntriesInDestination
            );

            var voidDest = new VoidDestination<string[]>();
            source.LinkTo(trans);
            trans.LinkTo(lookup);
            lookup.LinkTo(voidDest);

            source.Execute();
            voidDest.Wait();

            //Assert
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "dbo.HashMatchDestination",
                    $"id = 1 AND name='Bugs' AND age = 12 AND hashcode = '{HashHelper.Encrypt_Char40("1Bugs12")}'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    SqlConnection,
                    "dbo.HashMatchDestination",
                    $"id = 2 AND name='Coyote' AND age = 8 AND hashcode = '{HashHelper.Encrypt_Char40("2Coyote8")}'"
                )
            );
        }
    }
}

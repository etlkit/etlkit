using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;
using EtlKit.TestShared;

namespace EtlKit.TestDatabaseConnectors.DBDestination
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbDestinationStringArrayTests : DatabaseConnectorsTestBase
    {
        public DbDestinationStringArrayTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        public static IEnumerable<object[]> Connections => AllSqlConnections;

        [Theory, MemberData(nameof(AllConnectionsWithoutClickHouseWithPK))]
        public void WithSqlNotMatchingColumns(ConnectionManagerWithPK data)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(
                data.Connection,
                "SourceNotMatchingCols",
                data.WithPK
            );
            s2C.InsertTestData();
            SqlTask.ExecuteNonQuery(
                data.Connection,
                "Create destination table",
                $@"CREATE TABLE destination_notmatchingcols
                ( col3 VARCHAR(100) NULL
                , col4 VARCHAR(100) NULL
                , {s2C.QB}Col1{s2C.QE} VARCHAR(100) NULL)"
            );

            //Act
            DbSource<string[]> source = new DbSource<string[]>
            {
                Sql =
                    $"SELECT {s2C.QB}Col1{s2C.QE}, {s2C.QB}Col2{s2C.QE} FROM {s2C.QB}SourceNotMatchingCols{s2C.QE}",
                ConnectionManager = data.Connection,
            };
            DbDestination<string[]> dest = new DbDestination<string[]>(
                data.Connection,
                "destination_notmatchingcols"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(data.Connection, "destination_notmatchingcols"));
            Assert.Equal(
                1,
                RowCountTask.Count(
                    data.Connection,
                    "destination_notmatchingcols",
                    "col3 = '1' AND col4='Test1'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    data.Connection,
                    "destination_notmatchingcols",
                    "col3 = '2' AND col4='Test2'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    data.Connection,
                    "destination_notmatchingcols",
                    "col3 = '3' AND col4='Test3'"
                )
            );
        }

        [Theory, MemberData(nameof(Connections))]
        public void WithLessColumnsInDestination(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "SourceTwoColumns");
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(connection, "SourceTwoColumns");
            s2C.InsertTestData();
            DropTableTask.DropIfExists(connection, "destination_onecolumn");
            CreateTableTask.Create(
                connection,
                "destination_onecolumn",
                new List<EtlKit.TableColumn>()
                {
                    new EtlKit.TableColumn("colx", "VARCHAR(100)", false, true),
                }
            );

            //Act
            DbSource<string[]> source = new DbSource<string[]>(connection, "SourceTwoColumns");
            DbDestination<string[]> dest = new DbDestination<string[]>(
                connection,
                "destination_onecolumn"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(3, RowCountTask.Count(connection, "destination_onecolumn"));
            Assert.Equal(1, RowCountTask.Count(connection, "destination_onecolumn", "colx = '1'"));
            Assert.Equal(1, RowCountTask.Count(connection, "destination_onecolumn", "colx = '2'"));
            Assert.Equal(1, RowCountTask.Count(connection, "destination_onecolumn", "colx = '3'"));
        }

        [Theory, MemberData(nameof(Connections))]
        public void WithAdditionalNullableCol(IConnectionManager connection)
        {
            //Arrange
            DropTableTask.DropIfExists(connection, "source_additionalnullcol");
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(
                connection,
                "source_additionalnullcol"
            );
            s2C.InsertTestData();
            DropTableTask.DropIfExists(connection, "destination_additionalnullcol");
            CreateTableTask.Create(
                connection,
                "destination_additionalnullcol",
                new List<EtlKit.TableColumn>()
                {
                    new EtlKit.TableColumn("id", "INT", false, true),
                    new EtlKit.TableColumn("col1", "VARCHAR(100)", true),
                    new EtlKit.TableColumn("col2", "VARCHAR(100)", true),
                    new EtlKit.TableColumn("col3", "VARCHAR(100)", true),
                }
            );

            //Act
            DbSource<string[]> source = new DbSource<string[]>(
                connection,
                "source_additionalnullcol"
            );
            DbDestination<string[]> dest = new DbDestination<string[]>(
                connection,
                "destination_additionalnullcol"
            );
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            s2C.AssertTestData();
        }

        [Theory, MemberData(nameof(Connections))]
        public void WithAdditionalNotNullCol(IConnectionManager connection)
        {
            //Arrange
            TwoColumnsTableFixture s2C = new TwoColumnsTableFixture(
                connection,
                "source_additionalnotnullcol"
            );
            s2C.InsertTestData();
            DropTableTask.DropIfExists(connection, "destination_additionalnotnullcol");
            CreateTableTask.Create(
                connection,
                "destination_additionalnotnullcol",
                new List<EtlKit.TableColumn>()
                {
                    new EtlKit.TableColumn("id", "INT", false, true),
                    new EtlKit.TableColumn("col1", "VARCHAR(100)", true),
                    new EtlKit.TableColumn("col2", "VARCHAR(100)", true),
                    new EtlKit.TableColumn("col3", "VARCHAR(100)", false),
                }
            );

            //Act
            DbSource<string[]> source = new DbSource<string[]>(
                connection,
                "source_additionalnotnullcol"
            );
            DbDestination<string[]> dest = new DbDestination<string[]>(
                connection,
                "destination_additionalnotnullcol"
            );
            source.LinkTo(dest);

            if (
                connection.ConnectionManagerType == ConnectionManagerType.ClickHouse
                || connection.ConnectionManagerType == ConnectionManagerType.MySql
            )
            {
                source.Execute();
                dest.Wait();
                Assert.Equal(3, RowCountTask.Count(connection, "destination_additionalnotnullcol"));
            }
            else
            {
                Assert.Throws<AggregateException>(() =>
                {
                    source.Execute();
                    dest.Wait();
                });
            }
        }
    }
}

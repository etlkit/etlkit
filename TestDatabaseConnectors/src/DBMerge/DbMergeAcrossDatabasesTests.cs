using System.Diagnostics.CodeAnalysis;
using EtlKit;
using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestDatabaseConnectors.Fixtures;

namespace EtlKit.TestDatabaseConnectors.DBMerge
{
    [Collection(nameof(DataFlowSourceDestinationCollection))]
    public class DbMergeAcrossDatabasesTests : DatabaseConnectorsTestBase
    {
        public DbMergeAcrossDatabasesTests(DatabaseSourceDestinationFixture fixture)
            : base(fixture) { }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public class Name
        {
            public string ID { get; set; }

            public string FIRST_NAME { get; set; }

            public string LAST_NAME { get; set; }
        }

        public class People : MergeableRow
        {
            [CompareColumn]
            public string FirstName { get; set; }

            [CompareColumn]
            public string LastName { get; set; }

            [IdColumn]
            public string Id { get; set; }
        }

        [Theory, MemberData(nameof(MixedSourceDestinations))]
        public void Test(Type sourceConnectionType, Type destConnectionType)
        {
            //Arrange
            IConnectionManager sourceConnection = GetConnectionManager(
                sourceConnectionType,
                DatabaseSourceDestinationFixture.SourceConfigSection
            );
            IConnectionManager destConnection = GetConnectionManager(
                destConnectionType,
                DatabaseSourceDestinationFixture.DestinationConfigSection
            );
            CreateSourceAndDestinationTables(sourceConnection, destConnection);

            //Act
            var nameSource = new DbSource<Name>(sourceConnection, "Name");
            var personMerge = new DbMerge<People>(destConnection, "People");

            var transform = new RowTransformation<Name, People>(d => new People
            {
                FirstName = d.FIRST_NAME,
                LastName = d.LAST_NAME,
                Id = d.ID,
            });

            nameSource.LinkTo(transform);
            transform.LinkTo(personMerge);

            nameSource.Execute();
            personMerge.Wait();

            //Assert
            var qb = destConnection.QB;
            var qe = destConnection.QE;
            Assert.Equal(
                1,
                RowCountTask.Count(
                    destConnection,
                    "People",
                    $"{qb}Id{qe} = 1 AND {qb}FirstName{qe} = 'Bugs' AND {qb}LastName{qe} IS NULL"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    destConnection,
                    "People",
                    $"{qb}Id{qe} = 2 AND {qb}FirstName{qe} IS NULL AND {qb}LastName{qe} = 'Pig'"
                )
            );
            Assert.Equal(
                1,
                RowCountTask.Count(
                    destConnection,
                    "People",
                    $"{qb}Id{qe} = 3 AND {qb}FirstName{qe} = 'Franky' AND {qb}LastName{qe} IS NULL"
                )
            );
        }

        private static void CreateSourceAndDestinationTables(
            IConnectionManager sourceConnection,
            IConnectionManager destConnection
        )
        {
            DropTableTask.DropIfExists(sourceConnection, "Name");
            CreateTableTask.Create(
                sourceConnection,
                "Name",
                new List<TableColumn>
                {
                    new(nameof(Name.ID), "INT", false, true),
                    new(nameof(Name.FIRST_NAME), "NVARCHAR(100)", true),
                    new(nameof(Name.LAST_NAME), "NVARCHAR(100)", true),
                }
            );
            DropTableTask.DropIfExists(destConnection, "People");
            CreateTableTask.Create(
                destConnection,
                "People",
                new List<TableColumn>
                {
                    new(nameof(People.Id), "INT", false, true),
                    new(nameof(People.FirstName), "NVARCHAR(100)", true),
                    new(nameof(People.LastName), "NVARCHAR(100)", true),
                }
            );

            SqlTask.ExecuteNonQueryFormatted(
                sourceConnection,
                "Test data",
                $@"INSERT INTO {nameof(Name):q} ({nameof(Name.ID):q}, {nameof(Name.FIRST_NAME):q}, {nameof(Name.LAST_NAME):q})
                VALUES (1, 'Bugs', NULL),
                       (2, NULL, 'Pig'),
                       (3, 'Franky', NULL)"
            );
            SqlTask.ExecuteNonQueryFormatted(
                destConnection,
                "Test data",
                $@"INSERT INTO {nameof(People):q} ({nameof(People.Id):q}, {nameof(People.FirstName):q}, {nameof(People.LastName):q})
                VALUES (1, 'Bugs', NULL)"
            );
            SqlTask.ExecuteNonQueryFormatted(
                destConnection,
                "Test data",
                $@"INSERT INTO {nameof(People):q} ({nameof(People.Id):q}, {nameof(People.FirstName):q}, {nameof(People.LastName):q})
                VALUES (2, 'Peggy', NULL)"
            );
        }
    }
}

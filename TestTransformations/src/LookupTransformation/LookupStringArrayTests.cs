using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.LookupTransformation
{
    [Collection("Transformations")]
    public sealed class LookupStringArrayTests : TransformationsTestBase, IDisposable
    {
        private readonly CultureInfo _culture;
        public static IEnumerable<object[]> Connections => AllSqlConnections;

        public LookupStringArrayTests(TransformationsDatabaseFixture fixture)
            : base(fixture)
        {
            _culture = CultureInfo.CurrentCulture;
        }

        [Theory, MemberData(nameof(Connections))]
        public void SimpleLookupWithoutObject(IConnectionManager connection)
        {
            //Arrange
            CultureInfo.CurrentCulture = connection.ConnectionCulture;
            var source2Columns = new TwoColumnsTableFixture(connection, "SourceNonGenericLookup");
            source2Columns.InsertTestData();
            var dest4Columns = new FourColumnsTableFixture(
                connection,
                "DestinationNonGenericLookup",
                -1
            );
            var lookup4Columns = new FourColumnsTableFixture(connection, "LookupNonGeneric");
            lookup4Columns.InsertTestData();

            var source = new DbSource<string[]>(connection, "SourceNonGenericLookup");
            var dest = new DbDestination<string[]>(connection, "DestinationNonGenericLookup");

            //Act
            var lookupList = new List<string[]>();

            var lookupSource = new DbSource<string[]>(connection, "LookupNonGeneric");
            var lookup = new LookupTransformation<string[], string[]>(
                lookupSource,
                row =>
                {
                    Array.Resize(ref row, 4);
                    row[2] = lookupList
                        .Where(lkupRow => lkupRow[0] == row[0])
                        .Select(lkupRow => lkupRow[2])
                        .FirstOrDefault();
                    row[3] = lookupList
                        .Where(lkupRow => lkupRow[0] == row[0])
                        .Select(lkupRow => lkupRow[3])
                        .FirstOrDefault();
                    return row;
                },
                lookupList
            );

            source.LinkTo(lookup);
            lookup.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            dest4Columns.AssertTestData();
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
